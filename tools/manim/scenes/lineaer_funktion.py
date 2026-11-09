from manim import (
    DOWN,
    LEFT,
    RIGHT,
    UP,
    Axes,
    Create,
    Dot,
    FadeIn,
    Line,
    MathTex,
    Scene,
    Text,
    VGroup,
    Write,
)


class LineaerFunktion(Scene):
    """Vis hvad hældning 2 betyder, med både graf og formel."""

    def construct(self):
        blue = "#60A5FA"
        yellow = "#FCD34D"

        title = Text("En lineær funktion", font_size=44).to_edge(UP, buff=0.45)
        axes = Axes(
            x_range=[-1, 3, 1],
            y_range=[-1, 7, 1],
            x_length=5.0,
            y_length=4.6,
            axis_config={"color": "#94A3B8", "include_numbers": True, "font_size": 24},
            tips=False,
        ).shift(LEFT * 3.3 + DOWN * 0.5)
        axis_labels = axes.get_axis_labels(MathTex("x"), MathTex("y"))
        graph = axes.plot(lambda x: 2 * x + 1, x_range=[-0.9, 2.8], color=blue)

        formula = MathTex(r"f(x)=", "2", r"x+1", font_size=58)
        formula[1].set_color(yellow)
        formula.move_to(RIGHT * 3.1 + UP * 1.15)

        a = axes.c2p(1, 3)
        corner = axes.c2p(2, 3)
        b = axes.c2p(2, 5)
        run = Line(a, corner, color=yellow)
        rise = Line(corner, b, color=yellow)
        run_label = MathTex(r"\Delta x=1", font_size=28, color=yellow).next_to(run, DOWN, buff=0.16)
        rise_label = MathTex(r"\Delta y=2", font_size=28, color=yellow).next_to(rise, RIGHT, buff=0.16)
        points = VGroup(Dot(a, color=yellow), Dot(b, color=yellow)).set_z_index(1)

        explanation = Text(
            "Når x stiger med 1,\nstiger y med 2.",
            font_size=34,
            line_spacing=1.2,
        ).move_to(RIGHT * 3.1 + DOWN * 0.25)

        self.play(FadeIn(title), Create(axes), FadeIn(axis_labels), run_time=1.2)
        self.play(Create(graph), Write(formula), run_time=1.2)
        self.play(FadeIn(points), Create(run), FadeIn(run_label), run_time=0.8)
        self.play(Create(rise), FadeIn(rise_label), FadeIn(explanation), run_time=1.0)
        self.wait(3.0)
