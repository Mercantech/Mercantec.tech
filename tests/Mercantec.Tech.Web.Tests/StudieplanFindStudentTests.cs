using Mercantec.Tech.Web.Lectio;

namespace Mercantec.Tech.Web.Tests;

public class StudieplanFindStudentTests
{
    [Fact]
    public void FindStudentSnapshot_returns_matching_student_from_list()
    {
        const string json = """
            {
              "students": [
                {
                  "lectioId": "111",
                  "name": "Alex",
                  "className": "H2",
                  "vuf": { "earned": 10, "required": 20, "percent": 50, "ok": false },
                  "h6": { "name": "Web", "level": "H2", "startDate": "2026-01-01", "endDate": "2026-06-01" },
                  "workplace": { "name": "Acme" }
                },
                {
                  "lectioId": "222",
                  "name": "Maja",
                  "className": "H3",
                  "vuf": { "earned": 20, "required": 20, "percent": 100, "ok": true }
                }
              ]
            }
            """;

        var snap = Studieplan.FindStudentSnapshot(json, "222");

        Assert.NotNull(snap);
        Assert.Equal("222", snap.LectioId);
        Assert.Equal("Maja", snap.Name);
        Assert.Equal("H3", snap.ClassName);
        Assert.True(snap.Vuf.Ok);
        Assert.Equal(20, snap.Vuf.Earned);
    }

    [Fact]
    public void FindStudentSnapshot_returns_null_when_missing()
    {
        const string json = """{ "students": [ { "lectioId": "111", "name": "Alex" } ] }""";
        Assert.Null(Studieplan.FindStudentSnapshot(json, "999"));
    }
}
