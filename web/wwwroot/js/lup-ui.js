window.lupTheme = {
    get() {
        return localStorage.getItem("lup-theme") || "system";
    },
    set(value) {
        const theme = value || "system";
        localStorage.setItem("lup-theme", theme);
        document.documentElement.dataset.theme = theme;
    }
};

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
