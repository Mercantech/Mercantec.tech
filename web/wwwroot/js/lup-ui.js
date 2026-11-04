window.lupTheme = {
    get() {
        return localStorage.getItem("lup-theme") || "system";
    },
    resolved(theme) {
        const value = theme || this.get();
        if (value === "dark") return "dark";
        if (value === "light" || value === "rainbow") return "light";
        return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    },
    apply(theme) {
        const value = theme || this.get();
        document.documentElement.dataset.theme = value;
        document.documentElement.dataset.bsTheme = this.resolved(value);
    },
    set(value) {
        const theme = value || "system";
        localStorage.setItem("lup-theme", theme);
        this.apply(theme);
    }
};

window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => {
    if (window.lupTheme.get() === "system")
        window.lupTheme.apply("system");
});

window.lupCourse = {
    get() {
        return localStorage.getItem("lup-demo-course");
    },
    set(value) {
        localStorage.setItem("lup-demo-course", value || "");
    }
};

window.lupIcons = () => {
    if (window.lucide?.createIcons)
        window.lucide.createIcons();
};
