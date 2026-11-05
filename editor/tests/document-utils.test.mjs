import test from 'node:test';
import assert from 'node:assert/strict';
import { splitSlides, selectedMembers, pollUrl } from '../src/document-utils.mjs';

test('presentation removes class rosters including nested rosters without changing source', () => {
  const blocks = [{ type: 'paragraph', content: 'Introduktion', children: [{ type: 'classRoster', props: { membershipIds: 'secret' } }] }, { type: 'slideBreak' }, { type: 'classRoster' }, { type: 'poll', props: { pollId: 'a' } }];
  const slides = splitSlides(blocks);
  assert.equal(slides.length, 2);
  assert.deepEqual(slides[0][0].children, []);
  assert.deepEqual(slides[1], [blocks[3]]);
  assert.equal(blocks[0].children.length, 1);
});

test('an empty document stays navigable while blank slides are omitted', () => {
  assert.equal(splitSlides([]).length, 1);
  assert.equal(splitSlides([{type:'slideBreak'}]).length, 1);
  assert.deepEqual(splitSlides([{type:'classRoster'}, {type:'paragraph',content:[]}, {type:'slideBreak'}, {type:'heading',content:'Dagens øvelse'}]), [[{type:'heading',content:'Dagens øvelse'}]]);
});

test('roster choices resolve by stable membership ID against current authorized roster', () => {
  const roster = [{ id: 'one', displayName: 'Samme navn' }, { id: 'two', displayName: 'Samme navn' }];
  assert.deepEqual(selectedMembers(roster, 'two,unknown'), [roster[1]]);
  assert.deepEqual(selectedMembers(roster, ''), roster);
});

test('poll links only accept UUIDs, including presentation links', () => {
  const id = 'ba72e62b-e57e-43af-b655-f8cdd2aeacbe';
  assert.equal(pollUrl(id), '/polls/' + id);
  assert.equal(pollUrl(id, true), '/teacher/polls/' + id + '/present');
  assert.equal(pollUrl('javascript:alert(1)'), null);
});
