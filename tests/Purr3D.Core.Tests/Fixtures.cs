using System.Collections.Generic;
using Purr3D.Core;

namespace Purr3D.Core.Tests
{
    /// <summary>
    /// Shapes of real sandbox responses (2026-11-22). State names and groups are real;
    /// ids and titles are invented, because this repo is public.
    /// </summary>
    internal static class Fixtures
    {
        /// The common "New > Ready > In progress > Ready for test > Done > Archived" project, in the API's
        /// own (storage) order, with Triage sorted last. Plus an archived project, one with no name, and junk.
        public const string ProjectList = @"{
          ""items"": [
            {
              ""id"": ""p-admin"", ""prefix"": ""ADMIN"", ""name"": ""Nakom Admin"", ""description"": ""Admin tooling"",
              ""icon"": ""/icons/ADMIN.png"", ""archived"": false, ""labels"": [""aws""], ""metadata"": {""bauEnabled"": false},
              ""states"": [
                {""id"": ""s-done"",     ""name"": ""Done"",           ""group"": ""completed"", ""order"": 50000},
                {""id"": ""s-new"",      ""name"": ""New"",            ""group"": ""backlog"",   ""order"": 10000},
                {""id"": ""s-triage"",   ""name"": ""Triage"",         ""group"": ""backlog"",   ""order"": 70000},
                {""id"": ""s-ready"",    ""name"": ""Ready"",          ""group"": ""unstarted"", ""order"": 20000},
                {""id"": ""s-archived"", ""name"": ""Archived"",       ""group"": ""completed"", ""order"": 60000},
                {""id"": ""s-doing"",    ""name"": ""In progress"",    ""group"": ""started"",   ""order"": 30000},
                {""id"": ""s-test"",     ""name"": ""Ready for test"", ""group"": ""started"",   ""order"": 40000}
              ]
            },
            { ""id"": ""p-old"", ""prefix"": ""OLD"", ""name"": ""Old"", ""archived"": true, ""states"": [] },
            { ""id"": ""p-nameless"", ""prefix"": ""NONAME"", ""states"": [] },
            { ""name"": ""not a project: no id or prefix"" },
            ""not an object""
          ]
        }";

        public const string TaskPageWithCursor = @"{
          ""items"": [
            {""id"": ""t1"", ""ref"": ""ADMIN-1"", ""projectId"": ""p-admin"", ""title"": ""First"", ""stateId"": ""s-done"",
             ""priority"": ""none"", ""labels"": [""aws"", ""lambda""], ""version"": 3, ""relations"": [], ""links"": [], ""epics"": []},
            {""id"": ""t2"", ""ref"": ""ADMIN-2"", ""projectId"": ""p-admin"", ""title"": ""Second"", ""stateId"": ""s-doing"",
             ""priority"": ""high"", ""points"": 3, ""labels"": [], ""version"": 1},
            {""id"": ""t3"", ""title"": ""no ref, no stateId: skipped""},
            ""not an object""
          ],
          ""cursor"": ""eyJwayI6IkFETUlOIn0""
        }";

        public const string LastTaskPage = @"{""items"": [{""id"": ""t9"", ""ref"": ""ADMIN-9"", ""stateId"": ""s-new"", ""points"": null}], ""cursor"": null}";
        public const string LastTaskPageNoCursor = @"{""items"": []}";

        public const string TaskBody = @"{""id"": ""t2"", ""ref"": ""ADMIN-2"", ""projectId"": ""p-admin"", ""title"": ""Second"",
          ""stateId"": ""s-test"", ""priority"": ""high"", ""points"": 3, ""labels"": [], ""version"": 2}";

        public const string ErrorBody = @"{""error"": {""code"": ""NOT_FOUND"", ""message"": ""no project PURR3D""}}";

        public static PurrState State(string id, StateGroup group, int order) =>
            new PurrState { Id = id, Name = id, Group = group, Order = order };

        public static PurrProject Project(params PurrState[] states) =>
            new PurrProject { Id = "p", Prefix = "P", Name = "P", States = new List<PurrState>(states) };

        /// "Backlog > Todo > In Progress > Done > Cancelled > Triage" (18 of 45 projects).
        public static PurrProject Standard() => Project(
            State("backlog", StateGroup.Backlog, 10000),
            State("todo", StateGroup.Unstarted, 20000),
            State("doing", StateGroup.Started, 30000),
            State("done", StateGroup.Completed, 40000),
            State("cancelled", StateGroup.Cancelled, 50000),
            State("triage", StateGroup.Backlog, 60000));

        /// The blog-posts project: Published (completed) sorts first, then a run of backlog states.
        public static PurrProject Blog() => Project(
            State("published", StateGroup.Completed, 1000),
            State("review", StateGroup.Started, 2000),
            State("aws", StateGroup.Backlog, 3000),
            State("ideas", StateGroup.Backlog, 6000),
            State("triage", StateGroup.Backlog, 7000));

        /// "Backlog > Done > Triage": nothing in between.
        public static PurrProject Minimal() => Project(
            State("backlog", StateGroup.Backlog, 10000),
            State("done", StateGroup.Completed, 20000),
            State("triage", StateGroup.Backlog, 30000));
    }
}
