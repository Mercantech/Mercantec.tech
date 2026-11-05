using ColorCode;
using ColorCode.Common;

namespace Mercantec.Tech.Web.Courses;

/// <summary>
/// ColorCode-sprog for Razor/Blazor/cshtml.
/// Undgår named regex-groups — ColorCode's capture-tæller ignorerer dem og forskyder scopes.
/// </summary>
internal sealed class RazorLanguage : ILanguage
{
    private static readonly string CSharpScope = ScopeName.LanguagePrefix + LanguageId.CSharp;

    public string Id => "razor";
    public string Name => "Razor";
    public string CssClassName => "razor";
    public string FirstLinePattern => null!;

    public IList<LanguageRule> Rules { get; } =
    [
        // @* kommentar *@
        new LanguageRule(
            @"(?s)(@\*)(.*?)(\*@)",
            new Dictionary<int, string>
            {
                { 1, ScopeName.HtmlServerSideScript },
                { 2, ScopeName.HtmlComment },
                { 3, ScopeName.HtmlServerSideScript }
            }),

        // HTML-kommentar
        new LanguageRule(
            @"\<![ \r\n\t]*(--([^\-]|[\r\n]|-[^\-])*--[ \r\n\t]*)\>",
            new Dictionary<int, string> { { 0, ScopeName.HtmlComment } }),

        // @code / @functions — greedy til sidste } (typisk én blok til sidst i komponenten)
        new LanguageRule(
            @"(?s)(@(?:code|functions)\b\s*)(\{.*\})",
            new Dictionary<int, string>
            {
                { 1, ScopeName.HtmlServerSideScript },
                { 2, CSharpScope }
            }),

        // @{ ... } med én nestet brace-niveau
        new LanguageRule(
            @"(?s)(@\{)((?:[^{}]|\{[^{}]*\})*)(\})",
            new Dictionary<int, string>
            {
                { 1, ScopeName.HtmlServerSideScript },
                { 2, CSharpScope },
                { 3, ScopeName.HtmlServerSideScript }
            }),

        // @( ... ) med én nestet paren-niveau
        new LanguageRule(
            @"(?s)(@\()((?:[^()]|\([^()]*\))*)(\))",
            new Dictionary<int, string>
            {
                { 1, ScopeName.HtmlServerSideScript },
                { 2, CSharpScope },
                { 3, ScopeName.HtmlServerSideScript }
            }),

        // Direktiver (@page, @inject, …)
        new LanguageRule(
            @"(?x)(@(?:page|model|inject|using|inherits|namespace|implements|attribute|layout|typeparam|rendermode|section|preservewhitespace|addTagHelper|removeTagHelper|tagHelperPrefix))\b[^\r\n]*",
            new Dictionary<int, string> { { 0, ScopeName.HtmlServerSideScript } }),

        // Styringsstrukturer
        new LanguageRule(
            @"(?x)(@(?:if|else\s+if|else|for|foreach|while|switch|do|try|catch|finally|lock|await))\b",
            new Dictionary<int, string> { { 1, ScopeName.Keyword } }),

        // HTML/Blazor-tags (PascalCase-komponenter + @onclick/@bind-attributter) — før implicit @
        new LanguageRule(
            @"(?xis)(</?)
               (?: ([A-Za-z][A-Za-z0-9-]*)(:) )*
               ([A-Za-z][A-Za-z0-9._-]*)
               (?:
                  [\s\n]+(@?[A-Za-z][A-Za-z0-9_.:-]*)[\s\n]*(=)[\s\n]*([^\s\n""'/<]+?)
                 |[\s\n]+(@?[A-Za-z][A-Za-z0-9_.:-]*)[\s\n]*(=)[\s\n]*(""[^\n]*?"")
                 |[\s\n]+(@?[A-Za-z][A-Za-z0-9_.:-]*)[\s\n]*(=)[\s\n]*('[^\n]*?')
                 |[\s\n]+(@?[A-Za-z][A-Za-z0-9_.:-]*) )*
               [\s\n]*
               (/?>)",
            new Dictionary<int, string>
            {
                { 1, ScopeName.HtmlTagDelimiter },
                { 2, ScopeName.HtmlElementName },
                { 3, ScopeName.HtmlTagDelimiter },
                { 4, ScopeName.HtmlElementName },
                { 5, ScopeName.HtmlAttributeName },
                { 6, ScopeName.HtmlOperator },
                { 7, ScopeName.HtmlAttributeValue },
                { 8, ScopeName.HtmlAttributeName },
                { 9, ScopeName.HtmlOperator },
                { 10, ScopeName.HtmlAttributeValue },
                { 11, ScopeName.HtmlAttributeName },
                { 12, ScopeName.HtmlOperator },
                { 13, ScopeName.HtmlAttributeValue },
                { 14, ScopeName.HtmlAttributeName },
                { 15, ScopeName.HtmlTagDelimiter }
            }),

        // Implicit udtryk: @name / @foo.Bar
        new LanguageRule(
            @"(?<![\w])(@[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)",
            new Dictionary<int, string> { { 1, ScopeName.HtmlServerSideScript } }),

        new LanguageRule(
            @"(?i)&\#?[a-z0-9]+?;",
            new Dictionary<int, string> { { 0, ScopeName.HtmlEntity } }),
    ];

    public bool HasAlias(string lang) =>
        lang.Equals("razor", StringComparison.OrdinalIgnoreCase)
        || lang.Equals("blazor", StringComparison.OrdinalIgnoreCase)
        || lang.Equals("cshtml", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Name;
}
