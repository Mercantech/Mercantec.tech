"""Regression and pruning episodes for the silent decision-tree course."""

from manim import DOWN, LEFT, RIGHT, UP, Axes, DashedLine, Dot, Line, VGroup

from decision_tree_video import (
    AMBER,
    BLUE,
    GRID,
    INK,
    MUTED,
    PINK,
    TEAL,
    DTVideo,
)


REGRESSION_DATA = ((1, 2), (2, 3), (3, 9), (4, 10))


class Regressionstrae(DTVideo):
    number = 3
    topic = "Regressionstræer"

    def plot(self, split=None, levels=(), residuals=False, extrapolate=False):
        """Draw the observations and the leaf prediction as horizontal pieces."""
        axes = Axes(
            x_range=[0, 5, 1],
            y_range=[0, 11, 1],
            x_length=6.45,
            y_length=3.25,
            tips=False,
            axis_config={"color": MUTED, "include_ticks": False, "stroke_width": 2},
        )
        grid = VGroup(
            *(
                Line(axes.c2p(0, y), axes.c2p(5, y), color=GRID, stroke_width=1.15)
                for y in (2, 4, 6, 8, 10)
            ),
            *(
                Line(axes.c2p(x, 0), axes.c2p(x, 11), color=GRID, stroke_width=1.15)
                for x in (1, 2, 3, 4)
            ),
        )
        ticks = VGroup(
            *(
                self.label(str(x), 18, MUTED).next_to(axes.c2p(x, 0), DOWN, buff=0.12)
                for x in (1, 2, 3, 4)
            ),
            *(
                self.label(str(y), 18, MUTED).next_to(axes.c2p(0, y), LEFT, buff=0.12)
                for y in (2, 4, 6, 8, 10)
            ),
        )
        x_title = self.label("Belastning x", 19, MUTED, True).next_to(
            axes.c2p(5, 0), DOWN, buff=0.34
        ).align_to(axes.c2p(5, 0), RIGHT)
        y_title = self.label("Behandlingstid y", 19, MUTED, True).next_to(
            axes.c2p(0, 11), UP, buff=0.14
        ).align_to(axes.c2p(0, 11), LEFT)

        fit = VGroup()
        focus = None
        if split is not None:
            guide = DashedLine(
                axes.c2p(split, 0),
                axes.c2p(split, 11),
                color=PINK,
                stroke_width=2.4,
                dash_length=0.11,
            )
            value = self.label(str(split).replace(".", ","), 20, PINK, True)
            value.next_to(axes.c2p(split, 0), DOWN, buff=0.12)
            fit.add(guide, value)
            focus = guide

        boundaries = (0, 5) if split is None else (0, split, 5)
        for index, level in enumerate(levels):
            color = TEAL if index == 0 else AMBER
            start, end = boundaries[index : index + 2]
            piece = Line(axes.c2p(start, level), axes.c2p(end, level), color=color, stroke_width=4.5)
            label_x = start + (end - start) * (0.15 if index == 0 else 0.9)
            value = f"{level:.2f}".rstrip("0").rstrip(".").replace(".", ",")
            tag = self.label(value, 20, color, True).next_to(axes.c2p(label_x, level), UP, buff=0.10)
            fit.add(piece, tag)
            if focus is None:
                focus = piece

        if residuals:
            for x, y in REGRESSION_DATA:
                estimate = levels[0 if split is None or x <= split else 1]
                if y == estimate:
                    continue
                fit.add(
                    DashedLine(
                        axes.c2p(x, y),
                        axes.c2p(x, estimate),
                        color=MUTED,
                        stroke_width=2.5,
                        dash_length=0.09,
                    )
                )

        dots = VGroup(*(Dot(axes.c2p(x, y), radius=0.065, color=BLUE) for x, y in REGRESSION_DATA))
        if extrapolate:
            dots.add(
                Dot(axes.c2p(0.35, levels[0]), radius=0.085, color=TEAL),
                Dot(axes.c2p(4.75, levels[1]), radius=0.085, color=AMBER),
            )

        return VGroup(grid, axes, ticks, x_title, y_title, fit, dots), focus

    def with_formula(self, plot, latex):
        return VGroup(plot, self.formula(latex, 35)).arrange(DOWN, buff=0.16)

    def lesson(self):
        graph, _ = self.plot()
        self.step(
            "Fra belastning til behandlingstid",
            graph,
            "Fire målinger",
            (
                "Vi vil forudsige en talværdi for behandlingstid.",
                "Belastning 1, 2, 3 og 4 gav tiderne 2, 3, 9 og 10.",
            ),
            seconds=8,
        )
        graph, prediction = self.plot(levels=(6,), residuals=True)
        self.step(
            "Før et split giver roden ét bud",
            self.with_formula(graph, r"\hat y=6\qquad\mathrm{SSE}=16+9+9+16=50"),
            "Gennemsnittet 6",
            (
                "Kvadratfejl er den lodrette afstand til buddet, opløftet i anden.",
                "SSE er summen. MSE er gennemsnittet, altså 50/4 = 12,50.",
            ),
            seconds=10,
            focus=prediction,
        )
        graph, split = self.plot(split=2.5, levels=(2.5, 9.5), residuals=True)
        self.step(
            "Prøv et split ved 2,5",
            self.with_formula(graph, r"\mathrm{SSE}=4\cdot 0{,}5^2=1\qquad\mathrm{MSE}=\frac{1}{4}=0{,}25"),
            "To lokale gennemsnit",
            (
                "Til venstre ligger 2 og 3. Til højre ligger 9 og 10.",
                "Alle fire fejl har størrelse 0,5. Den vægtede MSE falder med 12,25.",
            ),
            seconds=10,
            focus=split,
        )
        graph, _ = self.plot(split=1.5, levels=(2, 22 / 3), residuals=True)
        self.step(
            "Flyt splittet til 1,5",
            self.with_formula(graph, r"\hat y_L=2\qquad\hat y_R=\frac{3+9+10}{3}=\frac{22}{3}"),
            "En svagere opdeling",
            (
                "Det ene blad er perfekt med én observation.",
                "Til højre er målingerne spredte. Samlet MSE bliver ca. 7,17.",
            ),
            seconds=9,
        )
        score = self.table(
            ("Grænse", "SSE i alt", "Vægtet MSE"),
            (("1,5", "28,67", "7,17"), ("2,5", "1,00", "0,25"), ("3,5", "28,67", "7,17")),
            widths=(2.0, 2.35, 2.5),
            row_height=0.6,
            highlight=(1,),
        )
        self.step(
            "Sammenlign alle tre kandidater",
            VGroup(score, self.formula(r"\mathrm{MSE}=\frac{\mathrm{SSE}_L+\mathrm{SSE}_R}{n}", 38))
            .arrange(DOWN, buff=0.48),
            "Laveste vægtede fejl",
            (
                "Midtpunkterne mellem forskellige x-værdier er 1,5, 2,5 og 3,5.",
                "Hver observation tæller lige. Det bedste split her er 2,5.",
            ),
            seconds=9,
        )
        graph, _ = self.plot(split=2.5, levels=(2.5, 9.5), extrapolate=True)
        self.step(
            "Forudsigelsen er en trappe",
            graph,
            "Konstant i hvert blad",
            (
                "Input under 1 går stadig til venstre blad. Input over 4 går til højre.",
                "Yderbladet fortsætter med sin konstante værdi uden for de målte x-værdier.",
            ),
            seconds=9,
        )
        question = self.table(
            ("Nyt input x", "Følg testen x ≤ 2,5", "Bud på y"),
            (("2,50", "?", "?"), ("2,51", "?", "?")),
            widths=(1.65, 3.35, 1.9),
            row_height=0.64,
        )
        self.step(
            "Prøv selv ved tærsklen",
            question,
            "Hvor ender inputtet?",
            ("Sæt eventuelt på pause. Find bladet og forudsigelsen for begge input.",),
            seconds=6,
        )
        answer = self.table(
            ("Nyt input x", "Følg testen x ≤ 2,5", "Bud på y"),
            (("2,50", "Ja, venstre blad", "2,5"), ("2,51", "Nej, højre blad", "9,5")),
            widths=(1.65, 3.35, 1.9),
            row_height=0.64,
            highlight=(0, 1),
        )
        self.step(
            "Facit",
            answer,
            "Små ændringer kan give spring",
            (
                "Lighed er med på venstre side i denne regel.",
                "0,01 mere i x skifter blad og giver et nyt konstant bud.",
            ),
            seconds=7,
        )


class OverfittingOgBeskaering(DTVideo):
    number = 4
    topic = "Overfitting og beskæring"

    def server_tree(self, split_again=True):
        root = self.card("CPU ≤ 50 %", "5 nej · 3 ja", width=2.85, color=BLUE)
        root.shift(UP * 1.52)
        left = self.card("Alarm nej", "4 nej · 0 ja", width=2.55, color=TEAL)
        left.shift(LEFT * 2.1)
        if split_again:
            right = self.card("Fejl/min ≤ 1", "1 nej · 3 ja", width=2.65, color=BLUE)
        else:
            right = self.card("Alarm ja", "1 nej · 3 ja", width=2.65, color=AMBER)
        right.shift(RIGHT * 1.52)
        first_edges = VGroup(self.arrow(root, left, "Ja"), self.arrow(root, right, "Nej"))
        tree = VGroup(first_edges, root, left, right)
        if split_again:
            low = self.card("Alarm nej", "1 nej · 0 ja", width=2.28, color=TEAL)
            high = self.card("Alarm ja", "0 nej · 3 ja", width=2.28, color=AMBER)
            low.shift(DOWN * 1.56 + RIGHT * 0.12)
            high.shift(DOWN * 1.56 + RIGHT * 2.93)
            tree.add(self.arrow(right, low, "Ja"), self.arrow(right, high, "Nej"), low, high)
        return tree, right

    def risk_table(self, alpha):
        if alpha == 0.20:
            costs = ("0,60000", "0,58750", "0,66875")
            best = (1,)
        else:
            costs = ("0,90000", "0,78750", "0,76875")
            best = (2,)
        return self.table(
            ("Træ", "Blade", "Urenhed", "Samlet pris"),
            (
                ("Hele træet", "3", "0", costs[0]),
                ("Kun CPU-split", "2", "0,18750", costs[1]),
                ("Kun rod", "1", "0,46875", costs[2]),
            ),
            widths=(2.45, 0.95, 1.45, 2.05),
            row_height=0.56,
            highlight=best,
        )

    def lesson(self):
        tree, _ = self.server_tree()
        self.step(
            "Tre rene blade på otte målinger",
            tree,
            "Perfekt på træningsdata",
            (
                "Servertræets tre blade er rene. Ét af dem indeholder kun én måling.",
                "Små blade kan følge støj, så undersøg træet på nye data.",
            ),
            seconds=8,
        )
        tree, leaf = self.server_tree(split_again=False)
        self.step(
            "Begræns væksten fra starten",
            tree,
            "To måder at stoppe et split",
            (
                "max_depth = 1 tillader kun ét split på hver sti.",
                "min_samples_leaf = 2 ville afvise højre grens foreslåede split i 1 og 3 målinger.",
            ),
            seconds=10,
            focus=leaf,
        )
        risk = self.formula(r"R_\alpha(T)=R(T)+\alpha\cdot\lvert\operatorname{blade}(T)\rvert", 43)
        terms = VGroup(
            self.card("R(T)", "Vægtet urenhed\ni bladene", width=3.13, height=1.6, color=TEAL),
            self.card("α", "Prisen for\nét blad", width=3.13, height=1.6, color=AMBER),
        ).arrange(RIGHT, buff=0.27)
        self.step(
            "Beskær efter træet er vokset",
            VGroup(risk, terms).arrange(DOWN, buff=0.42),
            "Cost-complexity pruning",
            (
                "Et nyt blad er kun attraktivt, hvis det sparer nok urenhed til at betale sin pris.",
                "ccp_alpha styrer denne pris i scikit-learn.",
            ),
            seconds=9,
        )
        table = self.table(
            ("Træ", "Blade", "Vægtet Gini"),
            (("Hele træet", "3", "0"), ("Kun CPU-split", "2", "0,1875"), ("Kun rod", "1", "0,46875")),
            widths=(3.05, 1.15, 2.7),
            row_height=0.58,
            highlight=(1,),
        )
        self.step(
            "Regn bladenes urenhed ud",
            VGroup(table, self.formula(r"R(T_{\mathrm{CPU}})=\frac{4}{8}\cdot 0+\frac{4}{8}\cdot 0{,}375=0{,}1875", 35))
            .arrange(DOWN, buff=0.36),
            "Vægt efter antal målinger",
            (
                "Efter CPU-splittet ligger fire målinger i hvert blad.",
                "Venstre blad er rent. Højre har én nej og tre ja og dermed Gini 0,375.",
            ),
            seconds=10,
        )
        self.step(
            "Ved α = 0,20 er ét split bedst",
            VGroup(
                self.risk_table(0.20),
                self.formula(r"0{,}1875+0{,}20\cdot2=0{,}5875", 40, TEAL),
            ).arrange(DOWN, buff=0.34),
            "Beskær højre gren",
            (
                "To blade koster lidt mindre end tre.",
                "Vi beholder CPU-reglen og lader det højre blad forudsige ja ud fra tre af fire målinger.",
            ),
            seconds=9,
        )
        self.step(
            "Ved α = 0,30 vinder roden",
            VGroup(
                self.risk_table(0.30),
                self.formula(r"0{,}46875+0{,}30\cdot1=0{,}76875", 40, TEAL),
            ).arrange(DOWN, buff=0.34),
            "En højere bladpris",
            (
                "Nu er selv CPU-splittet for dyrt i regnestykket.",
                "Træet har ét blad og vælger flertalsklassen nej for alle input.",
            ),
            seconds=9,
        )
        train = self.card("Træn kandidater", "Flere værdier af α", width=4.8, color=BLUE)
        validate = self.card("Sammenlign med CV", "Vælg α på valideringsfoldene", width=4.8, color=TEAL)
        test = self.card("Afslut med test", "Mål den valgte model", width=4.8, color=PINK)
        stages = VGroup(train, validate, test).arrange(DOWN, buff=0.44)
        workflow = VGroup(stages, self.arrow(train, validate), self.arrow(validate, test))
        self.step(
            "Vælg α efter validering",
            workflow,
            "Mål hvordan træet generaliserer",
            (
                "Krydsvalidering sammenligner beskårne træer på foldene.",
                "Genfit det valgte træ på hele træningssættet, og mål det på den separate test.",
            ),
            seconds=10,
        )
