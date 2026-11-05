using Markdig;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Courses;

/// <summary>Markdown → sikker HTML til kursusteori (ingen rå HTML fra kilden).</summary>
public static class CourseMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static MarkupString ToHtml(string? markdown) =>
        new(Markdown.ToHtml(markdown ?? string.Empty, Pipeline));
}
