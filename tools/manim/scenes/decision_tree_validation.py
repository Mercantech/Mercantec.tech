"""LUP Decision Trees: model selection and tree ensembles."""

from manim import DOWN, LEFT, RIGHT, UP, SurroundingRectangle, VGroup

from decision_tree_video import AMBER, BLUE, INK, MUTED, PINK, TEAL, DTVideo


class ValideringUdenLaekage(DTVideo):
    number = 5
    topic = "Validering uden datalækage"

    def lesson(self):
        data = self.card("Data med kendt svar", width=4.25).move_to(1.5 * UP)
        train = self.card("75 % · Træningssæt", "Træn og vælg model", width=3.05, color=TEAL)
        train.move_to(1.82 * LEFT + 0.35 * DOWN)
        test = self.card("25 % · Testsæt", "Åbnes til sidst", width=3.05, color=AMBER)
        test.move_to(1.82 * RIGHT + 0.35 * DOWN)
        start = VGroup(self.arrow(data, train), self.arrow(data, test), data, train, test)
        self.step(
            "Del data før du vælger model",
            start,
            "To roller",
            (
                "Brug træningssættet til at træne træet og vælge fx dybde.",
                "Gem testsættet til én slutmåling af den valgte model.",
            ),
            seconds=9,
            focus=test,
        )

        grid, final_fold = self._fold_grid()
        self.step(
            "5-fold krydsvalidering",
            grid,
            "Fem forsøg",
            (
                "Del træningssættet i fem bidder. Træn på fire og validér på den femte.",
                "Skift valideringsbid hver gang. Hver bid er ude én gang.",
            ),
            seconds=11,
            focus=final_fold,
        )

        pipeline, fit = self._pipeline()
        self.step(
            "Tilpas pipeline i hver fold",
            pipeline,
            "Kun foldtræning",
            (
                "Lær fx en median, og træn træet, på foldens træningsrækker.",
                "Brug de lærte trin uændret på valideringsrækkerne. Mål så scoren.",
            ),
            seconds=10,
            focus=fit,
        )

        imputation = self.table(
            ("Rækker", "Observeret x", "Median"),
            (
                ("Foldtræning", "2, 4, 6", "4"),
                ("Foldvalidering", "100, mangler", "Brug 4"),
                ("Blandet ved en fejl", "2, 4, 6, 100", "5"),
            ),
            widths=(2.75, 2.6, 1.6),
            row_height=0.64,
            highlight=(1,),
        )
        example = self.label("Manglende x i validering bliver til 4", 26, TEAL, True)
        example.next_to(imputation, DOWN, buff=0.35)
        self.step(
            "Et konkret eksempel på lækage",
            VGroup(imputation, example),
            "Medianen ændrer sig",
            (
                "100 er kun observeret i validering. Hvis den tæller med, ændres medianen til 5.",
                "Så påvirker valideringsdata allerede det, modellen lærer.",
            ),
            seconds=10,
            focus=example,
        )

        scores = self.table(
            ("Dybde", "Fem valideringsscorer", "Gns."),
            (
                ("2", "0,88  0,90  0,86  0,89  0,87", "0,880"),
                ("4", "0,90  0,93  0,89  0,92  0,91", "0,910"),
                ("12", "0,86  0,90  0,83  0,89  0,84", "0,864"),
            ),
            widths=(1.15, 4.45, 1.35),
            row_height=0.68,
            highlight=(1,),
        )
        caption = self.label("Illustrerede scores · højere er bedre", 21, MUTED)
        caption.next_to(scores, DOWN, buff=0.3)
        self.step(
            "Vælg dybde med samme folds",
            VGroup(scores, caption),
            "Gennemsnittet afgør",
            (
                "Sammenlign de tre dybder på præcis de samme fem valideringsfolds.",
                "Her giver dybde 4 højeste gennemsnit. Testscoren indgår først bagefter.",
            ),
            seconds=10,
        )

        leakage = self.table(
            ("Faldgrube", "Hvad gør du?"),
            (
                ("Alarm kvitteret bagefter", "Brug kun input fra prognosetidspunktet."),
                ("Samme server i begge sæt", "Hold serverens rækker samlet i én gruppe."),
                ("Fremtidige rækker i træning", "Træn på fortiden og validér på senere data."),
            ),
            widths=(3.0, 4.15),
            row_height=0.76,
        )
        self.step(
            "Vælg split efter virkeligheden",
            leakage,
            "Efterlign brugen",
            (
                "Gruppér, når testen skal måle præstation på fx helt nye servere.",
                "Ved tidsdata skal modellen kun lære fra rækker, der ligger før valideringen.",
            ),
            seconds=10,
        )

        select = self.card("Dybde 4", "Valgt med CV", width=2.05, color=BLUE).move_to(2.55 * LEFT)
        refit = self.card("Træn igen", "Hele træningssættet", width=2.05, color=TEAL)
        evaluate = self.card("Mål én gang", "Separat testsæt", width=2.05, color=AMBER).move_to(2.55 * RIGHT)
        steps = VGroup(
            self.arrow(select, refit, start_edge=RIGHT, end_edge=LEFT),
            self.arrow(refit, evaluate, start_edge=RIGHT, end_edge=LEFT),
            select, refit, evaluate,
        )
        self.step(
            "Træn igen og mål på test",
            steps,
            "Slutmålingen",
            (
                "Genfit den valgte pipeline på alle træningsrækker.",
                "Brug testen til at rapportere resultatet. Mål variationen på tværs af folds undervejs.",
            ),
            seconds=10,
            focus=evaluate,
        )

    def _fold_grid(self):
        columns = VGroup(*(self.label(letter, 24, MUTED, True) for letter in "ABCDE"))
        for column, letter in enumerate(columns):
            letter.move_to((column * 1.07 - 1.65) * RIGHT + 1.75 * UP)
        rows = VGroup()
        last = None
        for fold in range(5):
            y = 1.16 - 0.65 * fold
            title = self.label(f"Runde {fold + 1}", 24, INK)
            title.move_to(3.0 * LEFT + y * UP)
            cells = []
            for column in range(5):
                validation = fold == column
                color = AMBER if validation else TEAL
                tile = self.box(0.91, 0.48, stroke=color)
                marker = self.label("VAL" if validation else "TRÆN", 17, color, True)
                cell = VGroup(tile, marker).move_to((column * 1.07 - 1.65) * RIGHT + y * UP)
                cells.append(cell)
                if validation:
                    last = cell
            rows.add(VGroup(title, *cells))
        return VGroup(columns, rows), last

    def _pipeline(self):
        learn = self.card("Foldtræning", "Observerede rækker", width=2.4, color=TEAL)
        learn.move_to(2.48 * LEFT + 1.08 * UP)
        holdout = self.card("Foldvalidering", "Ukendte for modellen", width=2.4, color=AMBER)
        holdout.move_to(2.48 * LEFT + 0.92 * DOWN)
        fit = self.card("Lær trin", "Median og træ", width=2.16, color=TEAL)
        fit.move_to(0.08 * RIGHT + 1.08 * UP)
        transform = self.card("Brug lærte trin", "Ingen ny tilpasning", width=2.16, color=AMBER)
        transform.move_to(0.08 * RIGHT + 0.92 * DOWN)
        score = self.card("Score", "Mål validering", width=1.7, color=BLUE)
        score.move_to(2.77 * RIGHT + 0.92 * DOWN)
        edges = VGroup(
            self.arrow(learn, fit, start_edge=RIGHT, end_edge=LEFT),
            self.arrow(holdout, transform, start_edge=RIGHT, end_edge=LEFT),
            self.arrow(fit, transform),
            self.arrow(transform, score, start_edge=RIGHT, end_edge=LEFT),
        )
        return VGroup(edges, learn, holdout, fit, transform, score), fit


class RandomForestOgBoosting(DTVideo):
    number = 8
    topic = "Random Forest og XGBoost"

    def lesson(self):
        bootstrap, duplicate = self._bootstrap()
        self.step(
            "Random Forest starter med bootstrap",
            bootstrap,
            "Træk med tilbagelægning",
            (
                "Hvert træ får sit eget udtræk. En række kan dukke op flere gange eller slet ikke.",
                "Fx kan træ 1 vurdere række C uden at have set den i sin træning.",
            ),
            seconds=10,
            focus=duplicate,
        )

        features = self._features()
        self.step(
            "Hver knude ser kun nogle features",
            features,
            "Mindre ens træer",
            (
                "Udtrækket af kandidater sker igen ved hver knude.",
                "Sammen med bootstrap giver det træer, der ofte laver forskellige fejl.",
            ),
            seconds=9,
        )

        votes = self._probabilities()
        self.step(
            "Gennemsnit af sandsynligheder",
            votes,
            "Tre træer, ét svar",
            (
                "I scikit-learn gennemsnitter skoven træernes klasseandele fra deres blade.",
                "To træer foretrækker nej. Alligevel vinder ja med 58,3 % i gennemsnit.",
            ),
            seconds=10,
            focus=votes[-1],
        )

        initial = self.table(
            ("x", "Faktisk y", "Startbud", "Residual"),
            (
                ("1", "2", "6", "−4"),
                ("2", "4", "6", "−2"),
                ("3", "8", "6", "+2"),
                ("4", "10", "6", "+4"),
            ),
            widths=(1.0, 1.7, 1.85, 2.1),
            row_height=0.55,
        )
        mse = self.label("Start: gennemsnit 6 · MSE 10", 27, BLUE, True)
        mse.next_to(initial, DOWN, buff=0.32)
        self.step(
            "Boosting starter med ét bud",
            VGroup(initial, mse),
            "Fire tal, samme bud",
            (
                "Her starter modellen med gennemsnittet 6 for alle fire rækker.",
                "Residual er det faktiske tal minus modellens bud.",
            ),
            seconds=9,
        )

        root = self.card("x ≤ 2,5?", "Split på residualerne", width=3.3, color=BLUE).move_to(1.37 * UP)
        left = self.card("Rettelse −3", "Gns. af −4 og −2", width=2.75, color=PINK)
        left.move_to(1.72 * LEFT + 0.68 * DOWN)
        right = self.card("Rettelse +3", "Gns. af +2 og +4", width=2.75, color=TEAL)
        right.move_to(1.72 * RIGHT + 0.68 * DOWN)
        corrections = VGroup(self.arrow(root, left, "Ja"), self.arrow(root, right, "Nej"), root, left, right)
        self.step(
            "Træn et lille træ til rettelsen",
            corrections,
            "Kvadratisk tab",
            (
                "Ved halvt kvadrattab er den negative gradient netop residualen.",
                "I dette forenklede eksempel uden straf giver bladene rettelserne −3 og +3.",
            ),
            seconds=11,
            focus=root,
        )

        update, result = self._boost_update()
        self.step(
            "Læg en dæmpet rettelse til",
            update,
            "Læringsrate 0,5",
            (
                "Brug kun halvdelen af træets rettelse. Næste træ lærer af de nye residualer.",
                "XGBoost bruger også tabets krumning og regularisering til at styre rettelserne.",
            ),
            seconds=10,
            focus=result,
        )

        comparison = self.table(
            ("Model", "Opbygning", "Godt første valg til"),
            (
                ("Ét træ", "Forgrenede regler", "Et lille træ, du skal\nkunne forklare."),
                ("Random Forest", "Trænes hver for sig", "En stabil baseline\npå tabeldata."),
                ("XGBoost", "Træer tilføjes\nét ad gangen", "Tabeldata med plads\ntil tuning."),
            ),
            widths=(2.0, 2.35, 2.85),
            row_height=0.84,
        )
        self.step(
            "Sammenlign med samme validering",
            comparison,
            "Modelvalg",
            (
                "Brug samme folds og metrik. Vurdér også forklarbarhed, hastighed og kompleksitet.",
                "Mål den model, du vælger, på et separat testsæt.",
            ),
            seconds=10,
        )

    def _bootstrap(self):
        rows = VGroup()
        sample_rows = (
            ("Data", "ABCDEF"),
            ("Træ 1", "ABBDEF"),
            ("Træ 2", "AACDFF"),
            ("Træ 3", "BCDDEE"),
        )
        duplicate = None
        for row, (title, letters) in enumerate(sample_rows):
            y = 1.48 - row * 0.85
            heading = self.label(title, 29, INK, True).move_to(2.9 * LEFT + y * UP)
            tiles = VGroup()
            for column, letter in enumerate(letters):
                repeated = row > 0 and letters.count(letter) > 1
                color = AMBER if repeated else TEAL
                tile = self.box(0.65, 0.59, stroke=color)
                mark = self.label(letter, 27, color, True)
                cell = VGroup(tile, mark).move_to((column * 0.82 - 1.6) * RIGHT + y * UP)
                tiles.add(cell)
            if row == 1:
                duplicate = SurroundingRectangle(VGroup(tiles[1], tiles[2]), color=AMBER, buff=0.08)
            rows.add(VGroup(heading, tiles))
        legend = self.label("Gul kant markerer gentagne rækker", 23, MUTED)
        legend.next_to(rows, DOWN, buff=0.24)
        return VGroup(rows, legend, duplicate), duplicate

    def _features(self):
        feature_list = self.card("Mulige features", "CPU · fejl/min · svartid · trafik", width=5.6, color=BLUE)
        feature_list.move_to(1.42 * UP)
        candidates = (
            ("Knude A", "CPU · trafik", TEAL),
            ("Knude B", "Fejl/min · svartid", AMBER),
            ("Knude C", "CPU · fejl/min", PINK),
        )
        nodes = VGroup()
        for index, (name, body, color) in enumerate(candidates):
            node = self.card(name, body, width=2.16, color=color)
            node.move_to(((index - 1) * 2.38) * RIGHT + 0.61 * DOWN)
            nodes.add(node)
        arrows = VGroup(*(self.arrow(feature_list, node) for node in nodes))
        return VGroup(arrows, feature_list, nodes)

    def _probabilities(self):
        distribution = self.table(
            ("Træ", "Alarm: ja", "Alarm: nej"),
            (("1", "0,90", "0,10"), ("2", "0,45", "0,55"), ("3", "0,40", "0,60")),
            widths=(1.42, 2.15, 2.15),
            row_height=0.57,
        )
        calc = self.formula(r"(0{,}90+0{,}45+0{,}40)/3\approx 0{,}583", 37)
        calc.next_to(distribution, DOWN, buff=0.33)
        answer = self.label("Alarm: ja · 58,3 %", 27, TEAL, True)
        answer.next_to(calc, DOWN, buff=0.29)
        return VGroup(distribution, calc, answer)

    def _boost_update(self):
        formula = self.formula(r"F_1(x)=F_0(x)+0{,}5\,h_1(x)", 41, BLUE)
        values = self.table(
            ("x", "Tidligere", "Træets rettelse", "Nyt bud"),
            (
                ("1, 2", "6", "−3", "4,5"),
                ("3, 4", "6", "+3", "7,5"),
            ),
            widths=(1.04, 1.57, 2.45, 1.57),
            row_height=0.67,
        )
        values.next_to(formula, DOWN, buff=0.31)
        result = self.label("Trænings-MSE: 10 → 3,25", 29, TEAL, True)
        result.next_to(values, DOWN, buff=0.33)
        return VGroup(formula, values, result), result
