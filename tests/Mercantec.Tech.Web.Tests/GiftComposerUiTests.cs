using Bunit;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Gifts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Mercantec.Tech.Web.Tests;

public sealed class GiftComposerUiTests
{
    private static readonly GiftRecipient Sara = new(
        Guid.Parse("11111111-1111-4111-8111-111111111111"), "Sara Testperson", false);
    private static readonly GiftRecipient Emil = new(
        Guid.Parse("22222222-2222-4222-8222-222222222222"), "Emil Testperson", true);
    private static readonly IReadOnlyList<GiftRecipient> People = [Sara, Emil];

    [Fact]
    public void Gift_and_wrapping_choices_expose_one_lowercase_pressed_value_and_a_visible_selection_label()
    {
        using var context = new BunitContext();
        var composer = context.Render<GiftComposer>();

        foreach (var selector in new[] { ".gift-kind", ".gift-wrap-option" })
        {
            var choices = composer.FindAll(selector);
            Assert.Equal(3, choices.Count);
            Assert.All(choices, choice => Assert.Contains(choice.GetAttribute("aria-pressed"), new[] { "true", "false" }));
            var selected = Assert.Single(choices, choice => choice.GetAttribute("aria-pressed") == "true");
            Assert.Contains("Valgt", selected.TextContent);
        }

        Assert.Contains("Rød gave", composer.Find(".gift-wrap-option[aria-pressed=true]").TextContent);
        Assert.All(composer.FindAll(".gift-wrap-option"), choice => Assert.False(string.IsNullOrWhiteSpace(choice.TextContent)));
    }

    [Fact]
    public void Filtering_recipients_does_not_hide_the_current_recipient_from_the_selection_summary_or_review()
    {
        using var context = new BunitContext();
        var composer = context.Render<GiftComposer>(p => p
            .Add(x => x.Recipients, People)
            .Add(x => x.RecipientId, Sara.UserSub));

        composer.Find("input[type=search]").Input("Emil");

        Assert.DoesNotContain(Sara.DisplayName, string.Join(" ", composer.FindAll(".gift-recipient").Select(x => x.TextContent)));
        Assert.Contains(Emil.DisplayName, Assert.Single(composer.FindAll(".gift-recipient")).TextContent);
        Assert.Contains(Sara.DisplayName, composer.Find(".gift-recipient-selection").TextContent);
        Assert.Contains(Sara.DisplayName, composer.Find(".gift-review").TextContent);
        Assert.False(composer.Find(".gift-actions .gift-primary").HasAttribute("disabled"));
    }

    [Fact]
    public void The_review_matches_the_selected_recipient_gift_wrapping_and_message_and_encodes_user_content()
    {
        using var context = new BunitContext();
        const string message = "Tak for hjælpen <script>alert('test')</script> & godt arbejde!";
        var composer = context.Render<GiftComposer>(p => p
            .Add(x => x.Recipients, People)
            .Add(x => x.RecipientId, Sara.UserSub)
            .Add(x => x.Kind, "thanks")
            .Add(x => x.Wrapping, "pine")
            .Add(x => x.Message, message));

        var review = composer.Find(".gift-review");
        Assert.Contains(Sara.DisplayName, review.TextContent);
        Assert.Contains("Tak for hjælpen", review.TextContent);
        Assert.Contains("Grøn gave", review.TextContent);
        Assert.Equal(message, review.QuerySelector("blockquote")!.TextContent.Trim());
        Assert.Empty(review.QuerySelectorAll("script"));
        Assert.Empty(composer.FindAll("script"));
        Assert.Contains(Sara.DisplayName, composer.Find(".gift-actions .gift-primary").TextContent);
    }

    [Fact]
    public void Busy_state_disables_controls_and_guards_callbacks_even_when_events_are_dispatched_directly()
    {
        using var context = new BunitContext();
        var calls = new List<string>();
        var composer = context.Render<GiftComposer>(p => p
            .Add(x => x.Recipients, People)
            .Add(x => x.RecipientId, Sara.UserSub)
            .Add(x => x.Message, "Min oprindelige hilsen")
            .Add(x => x.Busy, true)
            .Add(x => x.RecipientIdChanged, _ => calls.Add("recipient"))
            .Add(x => x.KindChanged, _ => calls.Add("kind"))
            .Add(x => x.WrappingChanged, _ => calls.Add("wrapping"))
            .Add(x => x.MessageChanged, _ => calls.Add("message"))
            .Add(x => x.OnReload, () => calls.Add("reload"))
            .Add(x => x.OnPreview, () => calls.Add("preview"))
            .Add(x => x.OnSend, () => calls.Add("send")));

        Assert.All(composer.FindAll("input, textarea, button"), control => Assert.True(control.HasAttribute("disabled")));
        for (var index = 0; index < composer.FindAll("button").Count; index++)
            composer.FindAll("button")[index].TriggerEvent("onclick", new MouseEventArgs());
        for (var index = 0; index < composer.FindAll("input[type=radio]").Count; index++)
            composer.FindAll("input[type=radio]")[index].TriggerEvent("onchange", new ChangeEventArgs { Value = Emil.UserSub.ToString() });
        composer.Find("textarea").TriggerEvent("oninput", new ChangeEventArgs { Value = "En ændret hilsen" });
        composer.Find("input[type=search]").TriggerEvent("oninput", new ChangeEventArgs { Value = "ingen match" });

        Assert.Empty(calls);
        Assert.Equal(2, composer.FindAll(".gift-recipient").Count);
        Assert.Contains("Min oprindelige hilsen", composer.Find(".gift-review").TextContent);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Loading_and_connection_errors_are_not_presented_as_an_empty_recipient_list(bool loading, bool error)
    {
        using var context = new BunitContext();
        var composer = context.Render<GiftComposer>(p => p
            .Add(x => x.Loading, loading)
            .Add(x => x.HasError, error));

        Assert.DoesNotContain("Ingen modtagere endnu", composer.Markup);
        Assert.True(composer.Find(".gift-actions .gift-primary").HasAttribute("disabled"));
        if (loading) Assert.Contains("Henter modtagere", composer.Markup);
    }

    [Fact]
    public void An_unavailable_selected_recipient_cannot_be_sent_a_gift()
    {
        using var context = new BunitContext();
        var sent = false;
        var composer = context.Render<GiftComposer>(p => p
            .Add(x => x.Recipients, People)
            .Add(x => x.RecipientId, Guid.Parse("99999999-9999-4999-8999-999999999999"))
            .Add(x => x.OnSend, () => sent = true));

        var send = composer.Find(".gift-actions .gift-primary");
        Assert.True(send.HasAttribute("disabled"));
        send.TriggerEvent("onclick", new MouseEventArgs());
        Assert.False(sent);
    }

    [Fact]
    public void The_synthetic_composer_can_be_exported_for_offline_visual_review_without_account_or_api_access()
    {
        using var context = new BunitContext();
        var composer = context.Render<GiftComposer>(p => p
            .Add(x => x.Recipients, People)
            .Add(x => x.RecipientId, Sara.UserSub)
            .Add(x => x.Kind, "thanks")
            .Add(x => x.Wrapping, "pine")
            .Add(x => x.Message, "Tak for hjælpen med projektet. Det gjorde en forskel!")
            .Add(x => x.ScopeDescription, "Testvisning med syntetiske personer. Ingen gave bliver sendt."));

        Assert.Contains(Sara.DisplayName, composer.Find(".gift-review").TextContent);
        var destination = Environment.GetEnvironmentVariable("GIFT_COMPOSER_QA_HTML");
        if (string.IsNullOrWhiteSpace(destination)) return;
        var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
        if (directory is not null) Directory.CreateDirectory(directory);
        File.WriteAllText(destination, composer.Markup);
    }
}
