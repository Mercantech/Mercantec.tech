"""De første to lydløse film i LUPs serie om beslutningstræer."""

from manim import DOWN, LEFT, RIGHT, UP, Axes, Dot, Line, Polygon, VGroup

from decision_tree_video import AMBER, INK, MUTED, PINK, TEAL, DTVideo


SERVER_ROWS = (
    ("A", "20", "0", "Nej"),
    ("B", "25", "3", "Nej"),
    ("C", "35", "1", "Nej"),
    ("D", "45", "2", "Nej"),
    ("E", "55", "2", "Ja"),
    ("F", "65", "0", "Nej"),
    ("G", "75", "3", "Ja"),
    ("H", "85", "4", "Ja"),
)


class TraeOgForudsigelse(DTVideo):
    number = 1
    topic = "Læs et beslutningstræ"

    def server_tree(self, route=()):
        """Tegn træet med en fremhævet sti, hvis en måling er undervejs."""
        definitions = (
            ("CPU ≤ 50 %?", "Rod · dybde 0", (0, 1.6, 0)),
            ("Alarm: nej", "4 målinger · dybde 1", (-2.35, 0, 0)),
            ("Fejl/min ≤ 1?", "Indre knude · dybde 1", (1.62, 0, 0)),
            ("Alarm: nej", "1 måling · dybde 2", (0.15, -1.64, 0)),
            ("Alarm: ja", "3 målinger · dybde 2", (3.05, -1.64, 0)),
        )
        nodes = [
            self.card(
                title,
                body,
                width=2.65,
                height=0.98,
                color=AMBER if index in route else TEAL if index in (1, 3, 4) else INK,
            ).move_to(point)
            for index, (title, body, point) in enumerate(definitions)
        ]
        transitions = ((0, 1, "Ja"), (0, 2, "Nej"), (2, 3, "Ja"), (2, 4, "Nej"))
        active_edges = set(zip(route, route[1:]))
        edges = [
            self.arrow(
                nodes[start],
                nodes[end],
                condition,
                color=AMBER if (start, end) in active_edges else MUTED,
            )
            for start, end, condition in transitions
        ]
        return VGroup(*edges, *nodes)

    def decision_regions(self):
        axes = Axes(
            x_range=[0, 100, 25],
            y_range=[0, 5, 1],
            x_length=6.3,
            y_length=3.45,
            tips=False,
            axis_config={"color": MUTED, "stroke_width": 2},
        )

        def region(x0, x1, y0, y1, color):
            return Polygon(
                axes.c2p(x0, y0),
                axes.c2p(x1, y0),
                axes.c2p(x1, y1),
                axes.c2p(x0, y1),
                fill_color=color,
                fill_opacity=0.15,
                stroke_width=0,
            )

        backgrounds = VGroup(
            region(0, 50, 0, 5, TEAL),
            region(50, 100, 0, 1, TEAL),
            region(50, 100, 1, 5, PINK),
        )
        cuts = VGroup(
            Line(axes.c2p(50, 0), axes.c2p(50, 5), color=INK, stroke_width=2),
            Line(axes.c2p(50, 1), axes.c2p(100, 1), color=INK, stroke_width=2),
        )
        labels = VGroup(
            self.label("Alarm: nej", 27, TEAL, True).move_to(axes.c2p(25, 2.8)),
            self.label("Alarm: nej", 22, TEAL, True).move_to(axes.c2p(76, 0.49)),
            self.label("Alarm: ja", 27, PINK, True).move_to(axes.c2p(75, 3.5)),
            self.label("50", 22, INK).next_to(axes.c2p(50, 0), DOWN, buff=0.10),
            self.label("1", 22, INK).next_to(axes.c2p(0, 1), LEFT, buff=0.15),
            self.label("Fejl/min", 23, MUTED, True)
            .next_to(axes.c2p(0, 5), UP, buff=0.13)
            .align_to(axes.c2p(0, 5), LEFT),
            self.label("CPU (%)", 23, MUTED, True).next_to(axes.c2p(100, 0), DOWN, buff=0.15),
        )
        marker = Dot(axes.c2p(72, 2), radius=0.085, color=AMBER)
        marker_text = self.label("(72, 2)", 22, AMBER, True).next_to(marker, RIGHT, buff=0.10)
        return VGroup(backgrounds, axes, cuts, labels, marker, marker_text)

    def lesson(self):
        self.step(
            "Fra rod til blad",
            self.server_tree(),
            "Ét spørgsmål ad gangen",
            (
                "Roden er træets første knude.",
                "Indre knuder tester en værdi. Blade giver en forudsigelse.",
                "Dybden tæller kanter fra roden, som har dybde 0.",
            ),
            seconds=10,
        )
        self.step(
            "Trin 1 · Test CPU",
            self.server_tree((0, 2)),
            "Ny måling",
            (
                "CPU er 72 %, og serveren har 2 fejl pr. minut.",
                "72 ≤ 50 er falsk. Følg grenen mærket Nej.",
            ),
            seconds=8,
        )
        self.step(
            "Trin 2 · Test fejl pr. minut",
            self.server_tree((0, 2, 4)),
            "Forudsigelse",
            (
                "2 ≤ 1 er falsk. Følg Nej igen.",
                "Vi er ved et blad, der forudsiger alarm: ja.",
                "Stien svarer til CPU > 50 % OG fejl/min > 1.",
            ),
            seconds=9,
        )
        self.step(
            "Ved grænsen gælder ≤ stadig",
            self.server_tree((0, 1)),
            "Præcis 50 % CPU",
            (
                "50 ≤ 50 er sand. Følg Ja til venstre.",
                "Her er vi allerede ved et blad. Selv 9 fejl/min ændrer derfor ikke træets svar.",
            ),
            seconds=9,
        )
        self.step(
            "Stierne deler inputrummet",
            self.decision_regions(),
            "Ét blad pr. område",
            (
                "CPU-splittet tegner en lodret grænse.",
                "Fejlsplittet deler kun området til højre.",
                "Punktet (72, 2) ligger i området for alarm.",
            ),
            seconds=10,
        )
        self.step(
            "Prøv selv",
            self.table(
                ("CPU", "Fejl/min", "Alarm?"),
                (("50 %", "9", "?"), ("72 %", "1", "?"), ("72 %", "2", "?")),
                widths=(1.6, 2, 2.1),
                row_height=0.69,
            ),
            "Følg pilene",
            (
                "Brug først CPU-testen. Tjek kun antal fejl, hvis du når den indre knude.",
                "Find svarene før næste billede.",
            ),
            seconds=8,
        )
        self.step(
            "Facit · Tre målinger",
            self.table(
                ("CPU", "Fejl/min", "Alarm"),
                (("50 %", "9", "Nej"), ("72 %", "1", "Nej"), ("72 %", "2", "Ja")),
                widths=(1.6, 2, 2.1),
                row_height=0.69,
                highlight=(2,),
            ),
            "Lighed afgør to af svarene",
            (
                "50 % går til venstre i roden.",
                "72 % og 1 fejl/min går til venstre ved næste test.",
            ),
            seconds=8,
        )


class SplitOgUrenhed(DTVideo):
    number = 2
    topic = "Find det bedste split"

    def sample_groups(self):
        left = self.card("CPU ≤ 50 %", "4 nej · 0 ja", 3.15, 1.12, TEAL).move_to([-1.8, 0.1, 0])
        right = self.card("CPU > 50 %", "1 nej · 3 ja", 3.15, 1.12, PINK).move_to([1.8, 0.1, 0])
        root = self.card("CPU ≤ 50 %?", "5 nej · 3 ja", 3.3, 1.12, INK).move_to([0, 1.9, 0])
        weighted = self.formula(
            r"G_{\mathrm{split}}=\frac{4}{8}\cdot 0+\frac{4}{8}\cdot 0{,}375=0{,}1875",
            38,
            AMBER,
        ).move_to([0, -1.57, 0])
        return VGroup(
            self.arrow(root, left, "Ja", TEAL),
            self.arrow(root, right, "Nej", PINK),
            root,
            left,
            right,
            self.formula(r"G_L=0", 34, TEAL).move_to([-1.8, -0.83, 0]),
            self.formula(r"G_R=0{,}375", 34, PINK).move_to([1.8, -0.83, 0]),
            weighted,
        )

    def final_split(self):
        root = self.card("CPU ≤ 50 %?", "8 målinger", 2.65, 0.98, INK).move_to([0, 1.6, 0])
        quiet = self.card("Alarm: nej", "4 af 4", 2.65, 0.98, TEAL).move_to([-2.32, 0, 0])
        question = self.card("Fejl/min ≤ 1?", "1 nej · 3 ja", 2.65, 0.98, AMBER).move_to([1.65, 0, 0])
        low = self.card("Alarm: nej", "1 af 1", 2.65, 0.98, TEAL).move_to([0.18, -1.65, 0])
        high = self.card("Alarm: ja", "3 af 3", 2.65, 0.98, PINK).move_to([3.1, -1.65, 0])
        return VGroup(
            self.arrow(root, quiet, "Ja", TEAL),
            self.arrow(root, question, "Nej", AMBER),
            self.arrow(question, low, "Ja", TEAL),
            self.arrow(question, high, "Nej", PINK),
            root,
            quiet,
            question,
            low,
            high,
        )

    def lesson(self):
        self.step(
            "Otte konstruerede målinger",
            self.table(
                ("Række", "CPU %", "Fejl/min", "Alarm"),
                SERVER_ROWS,
                widths=(1.3, 1.6, 1.85, 1.55),
                row_height=0.43,
                highlight=(3, 4),
            ),
            "Data fra pensummets servercase",
            (
                "CPU og fejl/min er input. Alarm er det kendte svar under træning.",
                "Roden får alle otte rækker.",
            ),
            seconds=9,
        )
        midpoint = VGroup(
            self.label("To naboer i den sorterede CPU-kolonne", 29, MUTED, True),
            VGroup(
                self.card("45 %", "Række D", 2.4, 1.12, TEAL),
                self.card("55 %", "Række E", 2.4, 1.12, PINK),
            ).arrange(RIGHT, buff=0.58),
            self.formula(r"\frac{45+55}{2}=50", 50, AMBER),
            self.label("Venstre: CPU ≤ 50 %    ·    Højre: CPU > 50 %", 25, INK, True),
        ).arrange(DOWN, buff=0.43)
        self.step(
            "Find tærskler mellem naboværdier",
            midpoint,
            "Sådan prøver CART et split",
            (
                "Sortér de forskellige værdier i én inputkolonne.",
                "Midtpunktet mellem 45 og 55 er 50. Rækker med samme værdi lander på samme side.",
            ),
            seconds=9,
        )
        root_gini = VGroup(
            self.card("5 nej + 3 ja", "8 rækker i roden", 4.8, 1.05, INK),
            self.formula(r"G=1-\sum_k p_k^2", 45, MUTED),
            self.formula(r"G_{\mathrm{rod}}=1-\left(\frac{5}{8}\right)^2-\left(\frac{3}{8}\right)^2", 43),
            self.formula(r"G_{\mathrm{rod}}=0{,}46875", 52, AMBER),
        ).arrange(DOWN, buff=0.45)
        self.step(
            "Mål blandingen med Gini",
            root_gini,
            "Gini måler urenhed",
            (
                "p er andelen af en klasse i knuden.",
                "I en ren knude har én klasse andel 1. Derfor bliver Gini 0.",
            ),
            seconds=10,
        )
        self.step(
            "Vægt hvert barn efter størrelse",
            self.sample_groups(),
            "Kandidat · CPU ≤ 50 %",
            (
                "Venstre barn har kun nej. Til højre giver 1 nej og 3 ja Gini 0,375.",
                "Begge børn har 4 af 8 rækker og tæller derfor hver 4/8.",
            ),
            seconds=10,
        )
        candidates = VGroup(
            self.table(
                ("Kandidat", "Vægtet Gini", "Gevinst"),
                (
                    ("CPU ≤ 50 %", "0,1875", "0,28125"),
                    ("CPU ≤ 70 %", "0,20833", "0,26042"),
                    ("Fejl/min ≤ 2,5", "0,36667", "0,10208"),
                ),
                widths=(2.5, 2.13, 1.76),
                row_height=0.66,
                highlight=(0,),
            ),
            self.formula(r"\Delta G=0{,}46875-0{,}1875=0{,}28125", 37, AMBER),
        ).arrange(DOWN, buff=0.36)
        self.step(
            "Vælg den største gevinst",
            candidates,
            "CART vælger lokalt",
            (
                "Gevinst er rodens Gini minus vægtet Gini efter splittet.",
                "CPU ≤ 50 % vinder blandt alle 11 kandidater i denne knude.",
            ),
            seconds=10,
        )
        entropy = VGroup(
            self.formula(r"H=-\sum_k p_k\log_2(p_k)", 46, INK),
            self.formula(r"H_{\mathrm{rod}}\approx0{,}9544\ \mathrm{bit}", 40, MUTED),
            self.formula(r"H_{\mathrm{split}}\approx0{,}4056\ \mathrm{bit}", 40, MUTED),
            self.formula(r"\Delta H\approx0{,}5488\ \mathrm{bit}", 46, AMBER),
        ).arrange(DOWN, buff=0.42)
        self.step(
            "Entropi er et andet kriterium",
            entropy,
            "Samme opdeling, anden skala",
            (
                "Entropi måler usikkerhed i bit. Klasser med andel 0 bidrager med 0.",
                "Ved CPU ≤ 50 % bliver den vægtede entropi 0,4056 bit.",
            ),
            seconds=10,
        )
        self.step(
            "Gentag i det urene barn",
            self.final_split(),
            "Det næste split",
            (
                "Venstre er allerede rent. Søg videre i højre barn.",
                "Fejl/min ≤ 1 skiller F fra E, G og H. De tre blade er nu rene på træningsdata.",
            ),
            seconds=10,
        )
