// Specs for the pure halves of the Purr client: JSON parsing and state cycling.
// Run: Automation RunTests Purr3D   (see CLAUDE.md for the headless command)
//
// Fixtures follow the shapes of real sandbox responses (2026-11-22). State names and groups
// are real; ids and titles are invented, because this repo is public.

#if WITH_DEV_AUTOMATION_TESTS

#include "Misc/AutomationTest.h"
#include "PurrApi/PurrJson.h"
#include "PurrApi/PurrStateCycle.h"

namespace PurrFixtures
{
	// The common "New > Ready > In progress > Ready for test > Done > Archived" project (23 of 45),
	// in the API's own (storage) order, with Triage sorted last.
	const TCHAR* ProjectList = TEXT(R"json({
	  "items": [
	    {
	      "id": "p-admin", "prefix": "ADMIN", "name": "Nakom Admin", "description": "Admin tooling",
	      "icon": "/icons/ADMIN.png", "archived": false, "labels": ["aws"], "metadata": {"bauEnabled": false},
	      "states": [
	        {"id": "s-done",     "name": "Done",           "group": "completed", "order": 50000},
	        {"id": "s-new",      "name": "New",            "group": "backlog",   "order": 10000},
	        {"id": "s-triage",   "name": "Triage",         "group": "backlog",   "order": 70000},
	        {"id": "s-ready",    "name": "Ready",          "group": "unstarted", "order": 20000},
	        {"id": "s-archived", "name": "Archived",       "group": "completed", "order": 60000},
	        {"id": "s-doing",    "name": "In progress",    "group": "started",   "order": 30000},
	        {"id": "s-test",     "name": "Ready for test", "group": "started",   "order": 40000}
	      ]
	    },
	    {
	      "id": "p-old", "prefix": "OLD", "name": "Old", "archived": true, "states": []
	    },
	    {
	      "id": "p-nameless", "prefix": "NONAME", "states": []
	    },
	    {
	      "name": "not a project: no id or prefix"
	    }
	  ]
	})json");

	const TCHAR* TaskPageWithCursor = TEXT(R"json({
	  "items": [
	    {"id": "t1", "ref": "ADMIN-1", "projectId": "p-admin", "title": "First", "stateId": "s-done",
	     "priority": "none", "labels": ["aws", "lambda"], "version": 3, "relations": [], "links": [], "epics": []},
	    {"id": "t2", "ref": "ADMIN-2", "projectId": "p-admin", "title": "Second", "stateId": "s-doing",
	     "priority": "high", "points": 3, "labels": [], "version": 1},
	    {"id": "t3", "title": "no ref, no stateId: skipped"}
	  ],
	  "cursor": "eyJwayI6IkFETUlOIn0"
	})json");

	const TCHAR* LastTaskPage = TEXT(R"json({"items": [], "cursor": null})json");
	const TCHAR* LastTaskPageNoCursor = TEXT(R"json({"items": []})json");

	const TCHAR* ErrorBody = TEXT(R"json({"error": {"code": "NOT_FOUND", "message": "no project PURR3D"}})json");

	FPurrState State(const TCHAR* Id, const TCHAR* Name, EPurrStateGroup Group, int32 Order)
	{
		FPurrState S;
		S.Id = Id;
		S.Name = Name;
		S.Group = Group;
		S.Order = Order;
		return S;
	}

	FPurrProject Project(std::initializer_list<FPurrState> States)
	{
		FPurrProject P;
		P.Id = TEXT("p");
		P.Prefix = TEXT("P");
		P.States = States;
		return P;
	}

	using G = EPurrStateGroup;

	/** "Backlog > Todo > In Progress > Done > Cancelled > Triage" (18 of 45). */
	FPurrProject Standard()
	{
		return Project({
			State(TEXT("backlog"), TEXT("Backlog"), G::Backlog, 10000),
			State(TEXT("todo"), TEXT("Todo"), G::Unstarted, 20000),
			State(TEXT("doing"), TEXT("In Progress"), G::Started, 30000),
			State(TEXT("done"), TEXT("Done"), G::Completed, 40000),
			State(TEXT("cancelled"), TEXT("Cancelled"), G::Cancelled, 50000),
			State(TEXT("triage"), TEXT("Triage"), G::Backlog, 60000),
		});
	}

	/** The blog-posts project: Published (completed) sorts first, then a run of backlog states. */
	FPurrProject Blog()
	{
		return Project({
			State(TEXT("published"), TEXT("Published"), G::Completed, 1000),
			State(TEXT("review"), TEXT("Review"), G::Started, 2000),
			State(TEXT("aws"), TEXT("AWS & Infrastructure"), G::Backlog, 3000),
			State(TEXT("ideas"), TEXT("Ideas"), G::Backlog, 6000),
			State(TEXT("triage"), TEXT("Triage"), G::Backlog, 7000),
		});
	}

	/** "Backlog > Done > Triage": nothing in between. */
	FPurrProject Minimal()
	{
		return Project({
			State(TEXT("backlog"), TEXT("Backlog"), G::Backlog, 10000),
			State(TEXT("done"), TEXT("Done"), G::Completed, 20000),
			State(TEXT("triage"), TEXT("Triage"), G::Backlog, 30000),
		});
	}

	/** The id NextState moves From to, or "(stays)". */
	FString Next(const FPurrProject& P, const TCHAR* From)
	{
		FPurrState Out;
		return UPurrStateCycle::NextState(P, From, Out) ? Out.Id : FString(TEXT("(stays)"));
	}
}

BEGIN_DEFINE_SPEC(FPurrApiSpec, "Purr3D.PurrApi", EAutomationTestFlags::ProductFilter | EAutomationTestFlags::EditorContext)
END_DEFINE_SPEC(FPurrApiSpec)

void FPurrApiSpec::Define()
{
	using namespace PurrFixtures;

	Describe("ParseStateGroup", [this]()
	{
		It("maps every Purr group, and parks unknown ones in Backlog", [this]()
		{
			TestTrue("backlog", PurrJson::ParseStateGroup(TEXT("backlog")) == G::Backlog);
			TestTrue("unstarted", PurrJson::ParseStateGroup(TEXT("unstarted")) == G::Unstarted);
			TestTrue("started", PurrJson::ParseStateGroup(TEXT("started")) == G::Started);
			TestTrue("completed", PurrJson::ParseStateGroup(TEXT("completed")) == G::Completed);
			TestTrue("cancelled", PurrJson::ParseStateGroup(TEXT("cancelled")) == G::Cancelled);
			TestTrue("unknown", PurrJson::ParseStateGroup(TEXT("someday")) == G::Backlog);
		});
	});

	Describe("ParseProjectList", [this]()
	{
		It("parses projects, skips malformed ones, and sorts states by order", [this]()
		{
			TArray<FPurrProject> Projects;
			TestTrue("parsed", PurrJson::ParseProjectList(ProjectList, Projects));
			if (!TestEqual("three valid projects", Projects.Num(), 3)) return;

			const FPurrProject& Admin = Projects[0];
			TestEqual("prefix", Admin.Prefix, FString(TEXT("ADMIN")));
			TestEqual("name", Admin.Name, FString(TEXT("Nakom Admin")));
			TestEqual("icon", Admin.Icon, FString(TEXT("/icons/ADMIN.png")));
			TestFalse("not archived", Admin.bArchived);
			if (!TestEqual("seven states", Admin.States.Num(), 7)) return;
			TestEqual("first by order", Admin.States[0].Id, FString(TEXT("s-new")));
			TestEqual("second by order", Admin.States[1].Id, FString(TEXT("s-ready")));
			TestEqual("last by order", Admin.States[6].Id, FString(TEXT("s-triage")));
			TestEqual("order kept", Admin.States[4].Order, 50000);
			TestTrue("group parsed", Admin.States[4].Group == G::Completed);

			TestTrue("archived flag read", Projects[1].bArchived);
			TestEqual("name falls back to prefix", Projects[2].Name, FString(TEXT("NONAME")));
		});

		It("rejects a body that is not a project list", [this]()
		{
			TArray<FPurrProject> Projects;
			TestFalse("not JSON", PurrJson::ParseProjectList(TEXT("<html>"), Projects));
			TestFalse("no items", PurrJson::ParseProjectList(ErrorBody, Projects));
		});
	});

	Describe("ParseTaskPage", [this]()
	{
		It("parses tasks, optional points, labels and the cursor", [this]()
		{
			TArray<FPurrTask> Tasks;
			FString Cursor;
			TestTrue("parsed", PurrJson::ParseTaskPage(TaskPageWithCursor, Tasks, Cursor));
			TestEqual("cursor", Cursor, FString(TEXT("eyJwayI6IkFETUlOIn0")));
			if (!TestEqual("malformed task skipped", Tasks.Num(), 2)) return;

			TestEqual("ref", Tasks[0].Ref, FString(TEXT("ADMIN-1")));
			TestEqual("state", Tasks[0].StateId, FString(TEXT("s-done")));
			TestFalse("no points", Tasks[0].bHasPoints);
			TestEqual("labels", Tasks[0].Labels.Num(), 2);
			TestEqual("version", Tasks[0].Version, 3);

			TestTrue("has points", Tasks[1].bHasPoints);
			TestEqual("points", Tasks[1].Points, 3);
			TestEqual("priority", Tasks[1].Priority, FString(TEXT("high")));
		});

		It("treats a null or missing cursor as the last page", [this]()
		{
			TArray<FPurrTask> Tasks;
			FString Cursor = TEXT("stale");
			TestTrue("null cursor parsed", PurrJson::ParseTaskPage(LastTaskPage, Tasks, Cursor));
			TestTrue("null cursor is empty", Cursor.IsEmpty());
			Cursor = TEXT("stale");
			TestTrue("missing cursor parsed", PurrJson::ParseTaskPage(LastTaskPageNoCursor, Tasks, Cursor));
			TestTrue("missing cursor is empty", Cursor.IsEmpty());
		});
	});

	Describe("ParseError", [this]()
	{
		It("reads Purr's error envelope", [this]()
		{
			FString Code, Message;
			TestTrue("parsed", PurrJson::ParseError(ErrorBody, Code, Message));
			TestEqual("code", Code, FString(TEXT("NOT_FOUND")));
			TestEqual("message", Message, FString(TEXT("no project PURR3D")));
			TestFalse("not an error body", PurrJson::ParseError(TEXT(R"json({"items":[]})json"), Code, Message));
		});
	});

	Describe("NextState", [this]()
	{
		It("walks a standard project to Done and stops there", [this]()
		{
			const FPurrProject P = Standard();
			TestEqual("Backlog", Next(P, TEXT("backlog")), FString(TEXT("todo")));
			TestEqual("Todo", Next(P, TEXT("todo")), FString(TEXT("doing")));
			TestEqual("In Progress", Next(P, TEXT("doing")), FString(TEXT("done")));
			TestEqual("Done stays", Next(P, TEXT("done")), FString(TEXT("(stays)")));
		});

		It("never moves a task into Cancelled, and a cancelled task stays put", [this]()
		{
			const FPurrProject P = Standard();
			TestEqual("In Progress skips Cancelled", Next(P, TEXT("doing")), FString(TEXT("done")));
			TestEqual("Cancelled stays", Next(P, TEXT("cancelled")), FString(TEXT("(stays)")));
		});

		It("moves Triage (a backlog state sorted last) forward to the first unstarted state", [this]()
		{
			TestEqual("Triage", Next(Standard(), TEXT("triage")), FString(TEXT("todo")));
		});

		It("steps through several started states, then to the first completed state, not Archived", [this]()
		{
			TArray<FPurrProject> Parsed;
			PurrJson::ParseProjectList(ProjectList, Parsed);
			const FPurrProject& Admin = Parsed[0];
			TestEqual("New", Next(Admin, TEXT("s-new")), FString(TEXT("s-ready")));
			TestEqual("In progress", Next(Admin, TEXT("s-doing")), FString(TEXT("s-test")));
			TestEqual("Ready for test", Next(Admin, TEXT("s-test")), FString(TEXT("s-done")));
			TestEqual("Archived stays", Next(Admin, TEXT("s-archived")), FString(TEXT("(stays)")));
		});

		It("copes with a completed state sorted first (the blog project)", [this]()
		{
			const FPurrProject P = Blog();
			TestEqual("Ideas", Next(P, TEXT("ideas")), FString(TEXT("review")));
			TestEqual("AWS & Infrastructure", Next(P, TEXT("aws")), FString(TEXT("review")));
			TestEqual("Review", Next(P, TEXT("review")), FString(TEXT("published")));
			TestEqual("Published stays", Next(P, TEXT("published")), FString(TEXT("(stays)")));
		});

		It("goes straight from Backlog to Done when there is nothing in between", [this]()
		{
			const FPurrProject P = Minimal();
			TestEqual("Backlog", Next(P, TEXT("backlog")), FString(TEXT("done")));
			TestEqual("Triage", Next(P, TEXT("triage")), FString(TEXT("done")));
		});

		It("stays put for a state the project does not have", [this]()
		{
			TestEqual("unknown", Next(Standard(), TEXT("nope")), FString(TEXT("(stays)")));
			TestEqual("no states", Next(Project({}), TEXT("backlog")), FString(TEXT("(stays)")));
		});
	});
}

#endif // WITH_DEV_AUTOMATION_TESTS
