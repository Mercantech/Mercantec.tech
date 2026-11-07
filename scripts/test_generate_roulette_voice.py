"""Offline pipeline checks with synthetic waveforms, never published voice assets."""
from array import array
import json
import math
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import urllib.error

import generate_roulette_voice as bank


def synthetic_word():
    tone = [round(7000 * math.sin(index * 2 * math.pi * 240 / bank.SAMPLE_RATE))
            for index in range(bank.SAMPLE_RATE // 3)]
    return bank.encode_wav(array("h", [0] * bank.SAMPLE_RATE + tone + [0] * bank.SAMPLE_RATE))


class WordBankTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.output = Path(self.directory.name)
        self.clips = bank.words(bank.ROOT / "shared" / "roulette-voice-words.json")
        self.health = {"ready": True, "model": "test-only", "voiceId": "test-default", "arenaReference": False}

    def tearDown(self):
        self.directory.cleanup()

    def test_trims_silence_applies_arena_reflections_and_bounds_peak(self):
        source = synthetic_word()
        processed = bank.decode_wav(bank.prepare_word(source))
        self.assertLess(len(processed), bank.SAMPLE_RATE)
        self.assertGreater(len(processed), bank.SAMPLE_RATE // 2)
        self.assertEqual(0, processed[0])
        self.assertEqual(0, processed[-1])
        self.assertLessEqual(max(abs(value) for value in processed), 26028)
        self.assertGreater(max(abs(value) for value in processed), 25000)
        # Reflections extend beyond the retained 35 ms + word + 70 ms dry signal.
        dry_end = int(bank.SAMPLE_RATE * (0.035 + 1 / 3 + 0.080))
        self.assertGreater(max(abs(value) for value in processed[dry_end:]), 0)

    def test_refuses_silence_invalid_or_truncated_wave(self):
        with self.assertRaises(ValueError):
            bank.prepare_word(bank.encode_wav(array("h", [0] * bank.SAMPLE_RATE)))
        with self.assertRaises((ValueError, EOFError)):
            bank.decode_wav(synthetic_word()[:-7])
        with self.assertRaises(ValueError):
            bank.decode_wav(b"RIFF" + bytes(bank.MAX_BYTES))

    def test_catalog_has_only_the_48_expected_generic_words(self):
        self.assertEqual(48, len(self.clips))
        self.assertEqual({"id": "prefix-16", "text": "Neon", "file": "prefix-16.wav"}, self.clips[16])
        self.assertEqual("Opfinderne", self.clips[28]["text"])

    def test_completed_bank_can_resume_and_only_regenerates_corrupt_clip(self):
        with patch.object(bank, "request_json", return_value=synthetic_word()) as request:
            result = bank.build_bank("http://127.0.0.1:8091", self.output, self.clips, self.health)
            self.assertTrue(result["complete"])
            self.assertEqual(48, request.call_count)
            request.reset_mock()
            bank.build_bank("http://127.0.0.1:8091", self.output, self.clips, self.health)
            request.assert_not_called()
            (self.output / "prefix-16.wav").write_bytes(b"bad")
            bank.build_bank("http://127.0.0.1:8091", self.output, self.clips, self.health)
            request.assert_called_once_with("http://127.0.0.1:8091", "/speech", {"text": "Neon!", "style": "arena"})

    def test_interrupted_batch_never_advertises_a_complete_bank(self):
        with patch.object(bank, "request_json", side_effect=[synthetic_word(), synthetic_word(), urllib.error.URLError("offline")]):
            with self.assertRaises(urllib.error.URLError):
                bank.build_bank("http://127.0.0.1:8091", self.output, self.clips, self.health)
        result = json.loads((self.output / "manifest.json").read_text(encoding="utf-8"))
        self.assertFalse(result["complete"])
        self.assertEqual(["prefix-00", "prefix-01"], [clip["id"] for clip in result["clips"]])
        with patch.object(bank, "request_json", return_value=synthetic_word()) as request:
            resumed = bank.build_bank("http://127.0.0.1:8091", self.output, self.clips, self.health)
        self.assertTrue(resumed["complete"])
        self.assertEqual(46, request.call_count)

    def test_reference_or_processing_change_invalidates_resume_fingerprint(self):
        previous = bank.fingerprint(self.clips, self.health)
        self.assertNotEqual(previous, bank.fingerprint(self.clips, {**self.health, "voiceId": "another-reference"}))

    def test_raw_cache_reuses_model_output_but_respects_voice_text_profile_and_force(self):
        raw_cache = self.output / "private-raw"
        with patch.object(bank, "request_json", return_value=synthetic_word()) as request:
            original = bank.raw_word("http://127.0.0.1:8091", "Neon", self.health, raw_cache)
            self.assertEqual(original, bank.raw_word("http://127.0.0.1:8091", "Neon", self.health, raw_cache))
            request.assert_called_once()
            # Arena processing is separate and can run again without model inference.
            bank.prepare_word(original)
            self.assertEqual(original, bank.raw_word("http://127.0.0.1:8091", "Neon", self.health, raw_cache))
            request.assert_called_once()
            bank.raw_word("http://127.0.0.1:8091", "Opfinderne", self.health, raw_cache)
            bank.raw_word("http://127.0.0.1:8091", "Neon", {**self.health, "voiceId": "another-speaker"}, raw_cache)
            with patch.object(bank, "INFERENCE_PROFILE", "changed-inference-v2"):
                bank.raw_word("http://127.0.0.1:8091", "Neon", self.health, raw_cache)
            bank.raw_word("http://127.0.0.1:8091", "Neon", self.health, raw_cache, force=True)
            self.assertEqual(5, request.call_count)

    def test_bad_raw_cache_entry_is_regenerated(self):
        raw_cache = self.output / "private-raw"
        with patch.object(bank, "request_json", return_value=synthetic_word()) as request:
            bank.raw_word("http://127.0.0.1:8091", "Neon", self.health, raw_cache)
            next(raw_cache.glob("*.wav")).write_bytes(b"bad")
            result = bank.raw_word("http://127.0.0.1:8091", "Neon", self.health, raw_cache)
            self.assertEqual(2, request.call_count)
            self.assertGreater(len(bank.decode_wav(result)), 0)

    def test_only_loopback_bridge_without_credentials_or_redirects(self):
        self.assertEqual("http://127.0.0.1:8091", bank.validate_endpoint("http://127.0.0.1:8091/"))
        for value in ("https://127.0.0.1:8091", "http://example.com", "http://user:pass@127.0.0.1", "http://127.0.0.1/path"):
            with self.assertRaises(ValueError):
                bank.validate_endpoint(value)
        with self.assertRaises(ValueError):
            bank.NoRedirect().redirect_request(None, None, 302, "", {}, "http://example.com")


if __name__ == "__main__":
    unittest.main()
