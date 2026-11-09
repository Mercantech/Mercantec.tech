"""Python lab and classification evaluation in the Decision Trees series."""

import manimpango
from manim import DOWN, RIGHT, UP, Text, VGroup

from decision_tree_video import AMBER, BLUE, DTVideo, INK, MUTED, PINK, TEAL


class KlassifikationILab(DTVideo):
    number = 6
    topic = "Klassifikation i scikit-learn"

    def code(self, content, size=28):
        fonts = set(manimpango.list_fonts())
        font = next((name for name in ("Consolas", "DejaVu Sans Mono") if name in fonts), self.font)
        return Text(content, font=font, font_size=size, color=INK, line_spacing=1.1)

    def lesson(self):
        data = self.card("Wine-datasættet", "178 rækker · 13 målinger · 3 klasser", 5.05, 1.12, BLUE)
        train = self.card("133 til træning", "bruges til CV og modelvalg", 3.05, 1.05, TEAL)
        test = self.card("45 til sluttest", "holdes helt ude", 3.05, 1.05, AMBER)
        children = VGroup(train, test).arrange(RIGHT, buff=0.48)
        data.next_to(children, UP, buff=0.78)
        figure = VGroup(data, children, self.arrow(data, train), self.arrow(data, test))
        self.step(
            "Fra målinger til en klasse",
            figure,
            "Del data først",
            (
                "Wine følger med scikit-learn og kræver ingen download.",
                "Vi bruger 25 % til test. stratify=y bevarer klassernes forhold omtrent.",
            ),
            seconds=8,
            focus=test,
        )

        code = self.code(
            "pipeline = Pipeline([\n"
            '    ("impute", SimpleImputer(strategy="median")),\n'
            '    ("tree", DecisionTreeClassifier(random_state=42)),\n'
            "])",
            24,
        )
        route = VGroup(
            self.card("Træningsfold", "målte værdier", 2.0, 0.95, BLUE),
            self.card("Imputering", "find medianer", 2.0, 0.95, TEAL),
            self.card("Træ", "find split", 2.0, 0.95, TEAL),
        ).arrange(RIGHT, buff=0.42)
        arrows = VGroup(*(
            self.arrow(a, b, start_edge=RIGHT, end_edge=-RIGHT)
            for a, b in zip(route, route[1:])
        ))
        figure = VGroup(code, VGroup(route, arrows)).arrange(DOWN, buff=0.71)
        self.step(
            "Forbehandling hører til i pipeline",
            figure,
            "Tilpas på den aktuelle fold",
            (
                "Wine har ingen manglende værdier. Imputeringen viser, hvor det trin skal ligge.",
                "Valideringsrækker får de medianer, som træningsfolden allerede har lært.",
            ),
            seconds=9,
            focus=route[1],
        )

        grid = self.table(
            ("Parameter", "Kandidater"),
            (
                ("criterion", "gini · entropy"),
                ("max_depth", "2 · 4 · ingen grænse"),
                ("min_samples_leaf", "1 · 3 · 6"),
                ("ccp_alpha", "0 · 0,005 · 0,02"),
            ),
            widths=(3.05, 3.95),
            row_height=0.51,
        )
        cv = self.label("GridSearchCV · 5 stratificerede fold · balanced accuracy", 23, TEAL, True, 7.1)
        self.step(
            "Sammenlign træer på de samme fold",
            VGroup(grid, cv).arrange(DOWN, buff=0.31),
            "54 kombinationer",
            (
                "Hver kombination trænes og valideres fem gange.",
                "GridSearchCV vælger ud fra gennemsnitlig CV-score og genfitter på hele træningssættet.",
            ),
            seconds=10,
        )

        results = self.table(
            ("Resultat i eksempelkørslen", "Værdi"),
            (
                ("Kriterium / maksimal dybde", "entropi / 4"),
                ("Mindste blad / ccp_alpha", "1 / 0"),
                ("Balanced accuracy · CV", "0,926"),
                ("Balanced accuracy · test", "0,926"),
                ("Macro-F1 · test", "0,915"),
                ("Flertalsbaseline · test", "0,333"),
            ),
            widths=(4.54, 2.33),
            row_height=0.49,
            highlight=(3,),
        )
        self.step(
            "Mål på de 45 testrækker",
            results,
            "Vurdér mod en baseline",
            (
                "Balanced accuracy er gennemsnittet af recall for de tre klasser.",
                "Denne kørsel valgte dybde 4 og ingen efterbeskæring. Resultaterne kan variere.",
            ),
            seconds=10,
        )

        matrix = self.table(
            ("Faktisk ↓  /  Forudsagt →", "class_0", "class_1", "class_2"),
            (("class_0", "15", "0", "0"), ("class_1", "3", "14", "1"), ("class_2", "0", "0", "12")),
            widths=(2.92, 1.36, 1.36, 1.36),
            row_height=0.62,
            highlight=(1,),
        )
        recall = self.formula(r"\mathrm{recall}_{\mathrm{class\_1}}=\frac{14}{18}\approx77{,}8\%", 37, AMBER)
        self.step(
            "Hvilken klasse er sværest?",
            VGroup(matrix, recall).arrange(DOWN, buff=0.43),
            "Se på række class_1",
            (
                "Træet ramte 14 af 18 rigtige class_1. Tre blev til class_0 og én til class_2.",
                "Balanced accuracy giver den recall lige så stor vægt som de to klassers 100 %.",
            ),
            seconds=10,
        )

        nodes = VGroup(
            self.card("flavanoids: 2,980 > 1,575", "", 5.56, 0.69, BLUE),
            self.card("alcohol: 13,480 > 12,825", "", 5.56, 0.69, BLUE),
            self.card("alcalinity_of_ash: 20,500 ≤ 21,250", "", 5.56, 0.69, BLUE),
            self.card("Blad 9 · class_0", "Klasseandele [1, 0, 0]", 5.56, 0.84, TEAL),
        ).arrange(DOWN, buff=0.29)
        arrows = VGroup(*(self.arrow(a, b) for a, b in zip(nodes, nodes[1:])))
        self.step(
            "Følg én faktisk testrække",
            VGroup(nodes, arrows),
            "decision_path viser reglerne",
            (
                "Start ved rodens split. Følg uligheden med rækkens værdi og fortsæt til bladet.",
                "Her forudsiger træet class_0. Bladets andele gælder træningsrækkerne i bladet.",
            ),
            seconds=10,
            focus=nodes[-1],
        )

        commands = self.code(
            "tree.get_depth()       # 4\n"
            "tree.get_n_leaves()    # 7\n\n"
            "plot_tree(tree, max_depth=3)\n"
            "names = list(wine.feature_names)\n"
            "export_text(tree, feature_names=names)",
            28,
        )
        self.step(
            "Et billede kan vise mindre end modellen",
            commands,
            "Læs det fulde træ i tekst",
            (
                "plot_tree(max_depth=3) beskærer kun figuren.",
                "Træets hyperparameter max_depth styrer træningen. Brug export_text til alle regler.",
            ),
            seconds=9,
        )

        tasks = VGroup(
            self.card("1  Find en fejl", "Læs en celle uden for diagonalen.", 6.48, 0.99, BLUE),
            self.card("2  Spor rækken", "Find de mål, der sendte den til bladet.", 6.48, 0.99, TEAL),
            self.card("3  Sammenlign med CV", "Prøv dybde 2 og 4 på de samme fold.", 6.48, 0.99, AMBER),
        ).arrange(DOWN, buff=0.23)
        self.step(
            "Prøv selv i Wine-laboratoriet",
            tasks,
            "Brug koden på LUP-siden",
            (
                "Rækker i forvekslingsmatrixen er faktiske klasser. Kolonner er forudsigelser.",
                "Brug CV til nye valg. Gem en separat sluttest til den model, du vælger.",
            ),
            seconds=10,
        )


class BladandeleOgFejltyper(DTVideo):
    number = 7
    topic = "Bladandele og fejltyper"

    def lesson(self):
        top = self.card("Dybde 1 · CPU > 50 %", "Ét blad med 3 ja og 1 nej", 5.8, 1.05, BLUE)
        no = self.card("Fejl/min ≤ 1", "Ét blad med 0 ja og 1 nej", 3.22, 1.05, TEAL)
        yes = self.card("Fejl/min > 1", "Ét blad med 3 ja og 0 nej", 3.22, 1.05, AMBER)
        children = VGroup(no, yes).arrange(RIGHT, buff=0.46)
        top.next_to(children, UP, buff=0.75)
        self.step(
            "Hvad betyder 100 % i et blad?",
            VGroup(top, children, self.arrow(top, no), self.arrow(top, yes)),
            "Andelene kommer fra træningen",
            (
                "Med kun CPU-splittet er alarmandelen 3/4 = 75 % i højre blad.",
                "Efter næste split kan 3 ud af 3 give 100 %. Tre rækker er stadig et lille grundlag.",
            ),
            seconds=10,
            focus=yes,
        )

        probability = self.formula(r"\hat p(\mathrm{alarm}) = \frac{3}{4} = 0{,}75", 54, AMBER)
        rules = VGroup(
            self.card("Tærskel 0,50", "0,75 ≥ 0,50 → alarm", 3.13, 1.04, TEAL),
            self.card("Tærskel 0,80", "0,75 < 0,80 → ingen alarm", 3.13, 1.04, BLUE),
        ).arrange(RIGHT, buff=0.35)
        self.step(
            "Fra bladandel til alarmregel",
            VGroup(probability, rules).arrange(DOWN, buff=0.62),
            "Vælg en tærskel på validering",
            (
                "Standardvalget er klassen med den største andel. En egen tærskel kan ændre handlingen.",
                "En lavere tærskel fanger mindst lige så mange alarmer, men kan udløse flere falske.",
            ),
            seconds=10,
        )

        matrix = self.table(
            ("Faktisk ↓  /  Forudsagt →", "Ingen alarm", "Alarm"),
            (
                ("Ingen alarm (95)", "90 TN", "5 FP"),
                ("Alarm (5)", "1 FN", "4 TP"),
            ),
            widths=(3.02, 2.04, 1.92),
            row_height=0.66,
            highlight=(1,),
        )
        key = self.label("TN/TP = korrekt    ·    FP/FN = forkert", 24, MUTED)
        self.step(
            "Læs forvekslingsmatrixen",
            VGroup(matrix, key).arrange(DOWN, buff=0.38),
            "Start med den faktiske klasse",
            (
                "Fem normale målinger udløser en falsk alarm (FP).",
                "Fire rigtige alarmer fanges (TP). Én rigtig alarm overses (FN).",
            ),
            seconds=9,
            focus=matrix[-2],
        )

        equations = VGroup(
            self.formula(r"\mathrm{accuracy}=\frac{90+4}{100}=94\%", 36, BLUE),
            self.formula(r"\mathrm{precision}=\frac{4}{4+5}=\frac49\approx44{,}4\%", 36, AMBER),
            self.formula(r"\mathrm{recall}=\frac{4}{4+1}=\frac45=80\%", 36, TEAL),
            self.formula(r"F_1=\frac{2TP}{2TP+FP+FN}=\frac8{14}\approx57{,}1\%", 34, INK),
        ).arrange(DOWN, aligned_edge=-RIGHT, buff=0.31)
        self.step(
            "Regn på forskellige spørgsmål",
            equations,
            "Præcision og recall har hver sin nævner",
            (
                "Præcision ser kun på udløste alarmer. Recall ser på alle faktiske alarmer.",
                "F1 kombinerer de to. Accuracy tæller alle korrekte forudsigelser.",
            ),
            seconds=11,
        )

        compare = self.table(
            ("Model", "Accuracy", "Recall for alarm"),
            (
                ("Altid ingen alarm", "95 %", "0 %"),
                ("Alarmmodellen", "94 %", "80 %"),
            ),
            widths=(3.24, 1.48, 2.22),
            row_height=0.71,
            highlight=(1,),
        )
        self.step(
            "Den højeste accuracy kan skjule problemet",
            compare,
            "En overset alarm kan være dyr",
            (
                "Flertalsmodellen gætter rigtigt 95 gange, men fanger ingen af de fem alarmer.",
                "Sammenlign begge typer fejl, før du vælger en model eller en tærskel.",
            ),
            seconds=10,
        )

        costs = VGroup(
            self.formula(r"\mathrm{pris} = 10\cdot FN + 1\cdot FP", 50, AMBER),
            self.card("Altid ingen alarm", "5 oversete · 0 falske", 5.86, 0.93, BLUE),
            self.card("Alarmmodellen", "1 overset · 5 falske", 5.86, 0.93, TEAL),
        ).arrange(DOWN, buff=0.28)
        self.step(
            "Prøv selv: Hvad koster fejlene?",
            costs,
            "Regn prisen for hver model",
            (
                "I denne øvelse koster en overset alarm 10 point, og en falsk alarm koster 1.",
                "Sæt gerne på pause, og sammenlign de to samlede priser.",
            ),
            seconds=11,
        )

        answer = VGroup(
            self.formula(r"\mathrm{altid\ nej}: 10\cdot5+1\cdot0=50", 42, BLUE),
            self.formula(r"\mathrm{alarmmodel}: 10\cdot1+1\cdot5=15", 42, TEAL),
        ).arrange(DOWN, aligned_edge=-RIGHT, buff=0.46)
        self.step(
            "Facit: Alarmmodellen koster 15 point",
            answer,
            "Vælg fejl ud fra opgaven",
            (
                "I eksemplet er 15 mindre end 50, selv om alarmmodellen har lidt lavere accuracy.",
                "Tjek tærskel og sandsynligheder på data, som modellen ikke er trænet på.",
            ),
            seconds=10,
        )
