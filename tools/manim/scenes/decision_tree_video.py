"""Shared layout for the silent LUP Decision Trees videos."""

from textwrap import fill

import manimpango
from manim import (
    DOWN,
    LEFT,
    RIGHT,
    UP,
    Arrow,
    FadeIn,
    FadeOut,
    Indicate,
    Line,
    MathTex,
    Rectangle,
    RoundedRectangle,
    Scene,
    Text,
    Transform,
    VGroup,
)

NAVY = "#0D1727"
SURFACE = "#14253A"
GRID = "#2C435D"
INK = "#F2F7FC"
MUTED = "#B0C1D7"
TEAL = "#57DCCD"
AMBER = "#FFD078"
PINK = "#FF98AA"
BLUE = "#91BAFF"


class DTVideo(Scene):
    number = 0
    topic = ""

    def construct(self):
        self.camera.background_color = NAVY
        fonts = set(manimpango.list_fonts())
        self.font = next((name for name in ("DejaVu Sans", "Arial") if name in fonts), "")
        self.step_index = 0
        self.figure = None
        self.text_layer = None

        series = self.label("LUP  /  DECISION TREES", 20, TEAL, True)
        series.move_to([-6.46, 3.56, 0], LEFT)
        episode = self.label(f"{self.number:02d} / 08    {self.topic.upper()}", 19, MUTED, True, 7.7)
        episode.move_to([6.46, 3.56, 0], RIGHT)
        footer = self.label("UNDERVISNINGSSERIE  ·  DANSK TEKST", 16, MUTED, True)
        footer.move_to([-6.46, -3.57, 0], LEFT)
        self.add(
            series, episode, footer,
            Line([-6.46, 3.26, 0], [6.46, 3.26, 0], color=GRID, stroke_width=1.25),
            Line([-6.46, -3.28, 0], [6.46, -3.28, 0], color=GRID, stroke_width=1.25),
            self.box(7.76, 5.0, "#101D30").move_to([-2.57, -0.20, 0]),
            self.box(4.88, 5.0).move_to([4.02, -0.20, 0]),
        )
        self.lesson()

    def label(self, text, size=30, color=INK, bold=False, width=None):
        item = Text(
            str(text), font=self.font, font_size=size, color=color,
            weight="BOLD" if bold else "NORMAL", line_spacing=0.91,
        )
        if width is not None and item.width > width:
            item.scale_to_fit_width(width)
        return item

    def formula(self, latex, size=38, color=INK):
        return MathTex(latex, font_size=size, color=color)

    def box(self, width, height, fill=SURFACE, stroke=GRID):
        return RoundedRectangle(
            width=width, height=height, corner_radius=0.15,
            stroke_color=stroke, stroke_width=1.5, fill_color=fill, fill_opacity=1,
        )

    def card(self, title, body="", width=2.5, height=1.05, color=INK):
        frame = self.box(width, height, stroke=color)
        contents = VGroup(self.label(title, 26, color, True, width - 0.24))
        if body:
            contents.add(self.label(body, 21, MUTED, width=width - 0.24))
        contents.arrange(DOWN, buff=0.11)
        if contents.height > height - 0.23:
            contents.scale_to_fit_height(height - 0.23)
        return VGroup(frame, contents)

    def arrow(self, source, target, label="", color=MUTED, start_edge=DOWN, end_edge=UP):
        start = source.get_critical_point(start_edge) if hasattr(source, "get_critical_point") else source
        end = target.get_critical_point(end_edge) if hasattr(target, "get_critical_point") else target
        line = Arrow(
            start, end, buff=0.065, stroke_width=3.0,
            max_tip_length_to_length_ratio=0.14, color=color,
        )
        if not label:
            return line
        tag = self.label(label, 19, color, True, 1.5)
        tag.next_to(line.get_center(), RIGHT, buff=0.10)
        backdrop = self.box(tag.width + 0.13, tag.height + 0.08, "#101D30", "#101D30").move_to(tag)
        return VGroup(line, backdrop, tag)

    def table(self, headers, rows, widths=None, row_height=0.46, highlight=()):
        widths = widths or [6.9 / len(headers)] * len(headers)
        cells = VGroup()
        for i, row in enumerate((headers, *rows)):
            x = -sum(widths) / 2
            y = -i * row_height
            selected = i > 0 and i - 1 in highlight
            for value, width in zip(row, widths, strict=True):
                fill_color = "#1C4852" if selected else SURFACE if i == 0 else "#152337"
                cell = Rectangle(
                    width=width, height=row_height, fill_color=fill_color, fill_opacity=1,
                    stroke_color=GRID, stroke_width=1.3,
                ).move_to([x + width / 2, y, 0])
                color = TEAL if selected or i == 0 else INK
                text = self.label(value, 23 if i else 21, color, i == 0 or selected, width - 0.12)
                text.move_to(cell)
                cells.add(VGroup(cell, text))
                x += width
        return cells

    def step(self, title, figure, note_title, notes, seconds=8, focus=None, morph=False):
        """Show one complete, readable step before advancing."""
        self.step_index += 1
        if figure.width > 7.33:
            figure.scale_to_fit_width(7.33)
        if figure.height > 4.52:
            figure.scale_to_fit_height(4.52)
        figure.move_to([-2.57, -0.20, 0])

        heading = self.label(title, 44, bold=True, width=12.9)
        heading.move_to([-6.46, 2.77, 0], LEFT)
        step_index = self.label(f"TRIN  {self.step_index:02d}", 18, MUTED, True)
        step_index.move_to([6.45, -3.56, 0], RIGHT)

        kicker = self.label("FORKLARING", 18, TEAL, True)
        kicker.move_to([1.88, 1.97, 0], LEFT)
        lead = self.label(fill(note_title, 28, break_long_words=False), 29, INK, True, 4.29)
        body = VGroup(*(
            self.label(fill(note, 36, break_long_words=False), 25, MUTED, width=4.29)
            for note in notes
        )).arrange(DOWN, aligned_edge=LEFT, buff=0.29)
        explanation = VGroup(lead, body).arrange(DOWN, aligned_edge=LEFT, buff=0.34)
        if explanation.height > 3.95:
            explanation.scale_to_fit_height(3.95)
        explanation.move_to([1.88, 1.56, 0], LEFT + UP)
        new_text = VGroup(heading, step_index, kicker, explanation)

        animations = [FadeIn(new_text, shift=UP * 0.07)]
        if self.text_layer is not None:
            animations.append(FadeOut(self.text_layer))
        if self.figure is not None and morph:
            animations.append(Transform(self.figure, figure))
        else:
            if self.figure is not None:
                animations.append(FadeOut(self.figure))
            animations.append(FadeIn(figure, shift=UP * 0.10))
        self.play(*animations, run_time=0.64)
        if self.figure is not None and morph:
            self.remove(self.figure)
            self.add(figure)
        self.text_layer, self.figure = new_text, figure

        if focus is not None:
            targets = focus if isinstance(focus, (tuple, list)) else (focus,)
            self.play(*[Indicate(item, color=AMBER, scale_factor=1.035) for item in targets], run_time=0.61)

        words = sum(len(text.split()) for text in (note_title, *notes))
        self.wait(max(seconds, 1.3 + words * 0.35))
