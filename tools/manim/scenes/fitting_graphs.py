"""Fixed teaching data and reusable Manim plots for model fitting."""

import numpy as np
from manim import (
    DOWN,
    LEFT,
    RIGHT,
    UP,
    Axes,
    Circle,
    DashedLine,
    Dot,
    Line,
    Text,
    VGroup,
    VMobject,
)

NAVY = "#0D1727"
GRID = "#2B4359"
MUTED = "#AFC0D5"
CYAN = "#57DCCD"
GOLD = "#FFD078"
PINK = "#FF8EA3"
BLUE = "#8FB8FF"

MODEL_COLORS = {1: BLUE, 2: "#79D2FD", 6: PINK}

# Synthetic points near a parabola with fixed noise. The nine held-out points
# are independent of the seven points supplied to each least-squares fit.
TRAIN_X = np.linspace(-2, 2, 7)
TRAIN_NOISE = np.array([0.10, -0.48, 0.34, -0.38, 0.37, -0.42, 0.20])
VALIDATION_X = np.array([-1.8, -1.35, -0.95, -0.35, 0.2, 0.7, 1.15, 1.7, 1.9])
VALIDATION_NOISE = np.array([0.09, -0.08, 0.11, -0.09, 0.07, -0.1, 0.12, -0.12, 0.04])


def _signal(x):
    return 2.2 + 0.5 * x**2 + 0.23 * x


TRAIN_Y = _signal(TRAIN_X) + TRAIN_NOISE
VALIDATION_Y = _signal(VALIDATION_X) + VALIDATION_NOISE
POLYNOMIALS = {
    degree: np.polynomial.Polynomial.fit(TRAIN_X, TRAIN_Y, degree)
    for degree in MODEL_COLORS
}

# A separate illustrative neural-network history, not a fit to the points above.
EPOCHS = np.arange(1, 15)
TRAIN_LOSS = (1.18, 1.0, 0.86, 0.74, 0.64, 0.56, 0.48, 0.42, 0.37, 0.33, 0.30, 0.28, 0.26, 0.24)
VALIDATION_LOSS = (1.22, 1.05, 0.92, 0.82, 0.73, 0.67, 0.61, 0.59, 0.62, 0.67, 0.72, 0.79, 0.87, 0.96)
BEST_EPOCH = int(EPOCHS[np.argmin(VALIDATION_LOSS)])


def fit_rmse(degree):
    """Return training and validation RMSE for the same fitted polynomial."""
    model = POLYNOMIALS[degree]
    train_rmse = float(np.sqrt(np.mean((model(TRAIN_X) - TRAIN_Y) ** 2)))
    validation_rmse = float(np.sqrt(np.mean((model(VALIDATION_X) - VALIDATION_Y) ** 2)))
    return train_rmse, validation_rmse


def _decimal(value):
    return f"{value:g}".replace(".", ",").replace("-", "−")


def _line_path(axes, x, y, color, width=4):
    return (
        VMobject()
        .set_points_as_corners([axes.c2p(px, py) for px, py in zip(x, y)])
        .set_stroke(color, width=width)
        .set_z_index(2)
    )


class _GridPlot(VGroup):
    def __init__(self, width, height, x_ticks, y_ticks, x_label, y_label, font):
        super().__init__()
        self.font = font
        x_min, x_max = x_ticks[0], x_ticks[-1]
        y_min, y_max = y_ticks[0], y_ticks[-1]
        self.axes = Axes(
            x_range=[x_min, x_max, x_max - x_min],
            y_range=[y_min, y_max, y_max - y_min],
            x_length=width - 0.85,
            y_length=height - 1.16,
            tips=False,
            axis_config={"include_ticks": False, "stroke_opacity": 0},
        )
        axes = self.axes
        grid = VGroup(
            *(Line(axes.c2p(x_min, y), axes.c2p(x_max, y), color=GRID, stroke_width=1)
              for y in y_ticks),
            *(Line(axes.c2p(x, y_min), axes.c2p(x, y_max), color=GRID, stroke_width=1)
              for x in x_ticks),
        )
        frame = VGroup(
            Line(axes.c2p(x_min, y_min), axes.c2p(x_max, y_min), color=MUTED, stroke_width=1.6),
            Line(axes.c2p(x_min, y_min), axes.c2p(x_min, y_max), color=MUTED, stroke_width=1.6),
        )
        tick_labels = VGroup(
            *(self.label(_decimal(x), 18).next_to(axes.c2p(x, y_min), DOWN, buff=0.12)
              for x in x_ticks),
            *(self.label(_decimal(y), 18).next_to(axes.c2p(x_min, y), LEFT, buff=0.14)
              for y in y_ticks),
        )
        titles = VGroup(
            self.label(y_label, 20, bold=True)
            .next_to(axes.c2p(x_min, y_max), UP, buff=0.22)
            .align_to(axes.c2p(x_min, y_max), LEFT),
            self.label(x_label, 20, bold=True)
            .next_to(axes.c2p(x_max, y_min), DOWN, buff=0.42)
            .align_to(axes.c2p(x_max, y_min), RIGHT),
        )
        self.base = VGroup(axes, grid, frame, tick_labels, titles)
        self.add(self.base)
        self.legend_anchor = (x_max, y_max)

    def label(self, text, size, color=MUTED, bold=False):
        return Text(text, font=self.font, font_size=size, color=color, weight="BOLD" if bold else "NORMAL")

    def add_legend(self, entries):
        keys = VGroup(*[
            VGroup(symbol, self.label(name, 18)).arrange(RIGHT, buff=0.13)
            for symbol, name in entries
        ]).arrange(RIGHT, buff=0.36)
        anchor = self.axes.c2p(*self.legend_anchor)
        keys.next_to(anchor, UP, buff=0.20).align_to(anchor, RIGHT)
        self.base.add(keys)


def _validation_ring():
    return Circle(radius=0.082, stroke_color=GOLD, stroke_width=2.8, fill_color=NAVY, fill_opacity=1)


class FittingPlot(_GridPlot):
    def __init__(self, width=7.4, height=4.5, show_validation=True, font=""):
        super().__init__(width, height, (-2, -1, 0, 1, 2), (1, 2, 3, 4, 5), "Input, x", "Måling, y", font)
        self.training_dots = VGroup(*[
            Dot(self.axes.c2p(x, y), radius=0.080, color=CYAN, stroke_color=NAVY, stroke_width=1.4)
            for x, y in zip(TRAIN_X, TRAIN_Y)
        ]).set_z_index(4)
        self.validation_dots = VGroup(*[
            _validation_ring().move_to(self.axes.c2p(x, y))
            for x, y in zip(VALIDATION_X, VALIDATION_Y)
        ]).set_z_index(5)
        self.add_legend([(Dot(radius=0.073, color=CYAN), "Træning"), (_validation_ring(), "Validering")])
        if not show_validation:
            self.validation_dots.set_opacity(0)
        self.add(self.training_dots, self.validation_dots)

    def model_curve(self, degree):
        """Create a curve at the plot's current position."""
        x = np.linspace(TRAIN_X[0], TRAIN_X[-1], 241)
        return _line_path(self.axes, x, POLYNOMIALS[degree](x), MODEL_COLORS[degree])

    def residuals(self, degree, validation=True):
        x, y = (VALIDATION_X, VALIDATION_Y) if validation else (TRAIN_X, TRAIN_Y)
        return VGroup(*[
            DashedLine(
                self.axes.c2p(px, predicted),
                self.axes.c2p(px, actual),
                color=GOLD if validation else CYAN,
                stroke_width=2,
                stroke_opacity=0.7,
                dash_length=0.07,
            )
            for px, actual, predicted in zip(x, y, POLYNOMIALS[degree](x))
            if abs(actual - predicted) > 0.015
        ]).set_z_index(1)


class EarlyStoppingPlot(_GridPlot):
    checkpoint_epoch = BEST_EPOCH

    def __init__(self, width=8.0, height=4.5, font=""):
        super().__init__(width, height, (1, 4, 8, 12, 14), (0, 0.4, 0.8, 1.2, 1.4), "Epoker", "Tab", font)
        self.training_curve = _line_path(self.axes, EPOCHS, TRAIN_LOSS, CYAN)
        self.validation_curve = _line_path(self.axes, EPOCHS, VALIDATION_LOSS, GOLD)
        checkpoint = self.axes.c2p(BEST_EPOCH, VALIDATION_LOSS[BEST_EPOCH - 1])
        self.best_marker = VGroup(
            DashedLine(
                self.axes.c2p(BEST_EPOCH, 0), checkpoint, color=GOLD,
                stroke_width=2.4, dash_length=0.09,
            ),
            Dot(checkpoint, radius=0.095, color=GOLD, stroke_color=NAVY, stroke_width=2),
            self.label(f"Epoke {BEST_EPOCH} · laveste valideringstab", 19, GOLD, bold=True)
            .next_to(checkpoint, UP, buff=0.76),
        ).set_z_index(4)
        self.add_legend([
            (Line(LEFT * 0.13, RIGHT * 0.13, color=CYAN, stroke_width=4), "Træning"),
            (Line(LEFT * 0.13, RIGHT * 0.13, color=GOLD, stroke_width=4), "Validering"),
        ])
        self.add(self.training_curve, self.validation_curve, self.best_marker)
