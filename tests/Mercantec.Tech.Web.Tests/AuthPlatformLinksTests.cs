using Mercantec.Tech.Web.Lectio;

namespace Mercantec.Tech.Web.Tests;

public class AuthPlatformLinksTests
{
    [Fact]
    public void MapByAuthSub_links_auth_users_to_lectio_students()
    {
        const string students = """
            {
              "students": [
                { "lectioId": "111", "name": "Alex", "className": "H2" },
                { "lectioId": "222", "name": "Maja", "className": "H3" }
              ]
            }
            """;
        const string links = """
            {
              "links": [
                { "lectioId": "222", "authSub": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" },
                { "lectioId": "999", "authSub": "11111111-1111-1111-1111-111111111111" }
              ]
            }
            """;

        var map = AuthPlatformLinks.MapByAuthSub(students, links);

        Assert.Equal(2, map.Count);
        Assert.True(map.TryGetValue(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), out var maja));
        Assert.Equal("222", maja.LectioId);
        Assert.Equal("Maja", maja.Name);
        Assert.Equal("H3", maja.ClassName);

        Assert.True(map.TryGetValue(Guid.Parse("11111111-1111-1111-1111-111111111111"), out var orphan));
        Assert.Equal("999", orphan.LectioId);
        Assert.Null(orphan.Name);
    }

    [Fact]
    public void ExtractLectioIds_reads_students_array()
    {
        const string students = """
            { "students": [ { "lectioId": "a" }, { "lectioId": "b" }, { "name": "uden-id" } ] }
            """;

        var ids = AuthPlatformLinks.ExtractLectioIds(students);

        Assert.Equal(["a", "b"], ids);
    }

    [Fact]
    public void MapByAuthSub_accepts_bare_links_array()
    {
        const string students = """{ "students": [ { "lectioId": "x", "name": "X" } ] }""";
        const string links = """[ { "lectioId": "x", "authSub": "22222222-2222-2222-2222-222222222222" } ]""";

        var map = AuthPlatformLinks.MapByAuthSub(students, links);

        Assert.Single(map);
        Assert.Equal("x", map[Guid.Parse("22222222-2222-2222-2222-222222222222")].LectioId);
    }
}
