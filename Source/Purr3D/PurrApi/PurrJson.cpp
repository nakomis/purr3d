#include "PurrJson.h"

#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

namespace
{
	TSharedPtr<FJsonObject> ReadObject(const FString& Body)
	{
		TSharedPtr<FJsonObject> Root;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Body);
		if (!FJsonSerializer::Deserialize(Reader, Root) || !Root.IsValid())
		{
			return nullptr;
		}
		return Root;
	}

	FString StringOr(const TSharedPtr<FJsonObject>& Json, const TCHAR* Field, const FString& Default = FString())
	{
		FString Value;
		return Json->TryGetStringField(Field, Value) ? Value : Default;
	}

	int32 IntOr(const TSharedPtr<FJsonObject>& Json, const TCHAR* Field, int32 Default = 0)
	{
		double Value = 0.0;
		return Json->TryGetNumberField(Field, Value) ? FMath::RoundToInt32(Value) : Default;
	}

	bool ParseState(const TSharedPtr<FJsonObject>& Json, FPurrState& Out)
	{
		if (!Json.IsValid() || !Json->TryGetStringField(TEXT("id"), Out.Id))
		{
			return false;
		}
		Out.Name = StringOr(Json, TEXT("name"));
		Out.Group = PurrJson::ParseStateGroup(StringOr(Json, TEXT("group")));
		Out.Order = IntOr(Json, TEXT("order"));
		return true;
	}
}

EPurrStateGroup PurrJson::ParseStateGroup(const FString& Group)
{
	if (Group == TEXT("unstarted")) return EPurrStateGroup::Unstarted;
	if (Group == TEXT("started")) return EPurrStateGroup::Started;
	if (Group == TEXT("completed")) return EPurrStateGroup::Completed;
	if (Group == TEXT("cancelled")) return EPurrStateGroup::Cancelled;
	return EPurrStateGroup::Backlog;
}

bool PurrJson::ParseProject(const TSharedPtr<FJsonObject>& Json, FPurrProject& Out)
{
	if (!Json.IsValid() || !Json->TryGetStringField(TEXT("id"), Out.Id) || !Json->TryGetStringField(TEXT("prefix"), Out.Prefix))
	{
		return false;
	}
	Out.Name = StringOr(Json, TEXT("name"), Out.Prefix);
	Out.Description = StringOr(Json, TEXT("description"));
	Out.Icon = StringOr(Json, TEXT("icon"));
	Json->TryGetBoolField(TEXT("archived"), Out.bArchived);

	Out.States.Reset();
	const TArray<TSharedPtr<FJsonValue>>* States = nullptr;
	if (Json->TryGetArrayField(TEXT("states"), States))
	{
		for (const TSharedPtr<FJsonValue>& Value : *States)
		{
			FPurrState State;
			if (ParseState(Value->AsObject(), State))
			{
				Out.States.Add(State);
			}
		}
	}
	// The API returns states in storage order, not Purr order.
	Out.States.StableSort([](const FPurrState& A, const FPurrState& B) { return A.Order < B.Order; });
	return true;
}

bool PurrJson::ParseTask(const TSharedPtr<FJsonObject>& Json, FPurrTask& Out)
{
	if (!Json.IsValid() || !Json->TryGetStringField(TEXT("ref"), Out.Ref) || !Json->TryGetStringField(TEXT("stateId"), Out.StateId))
	{
		return false;
	}
	Out.Id = StringOr(Json, TEXT("id"));
	Out.ProjectId = StringOr(Json, TEXT("projectId"));
	Out.Title = StringOr(Json, TEXT("title"));
	Out.Priority = StringOr(Json, TEXT("priority"), TEXT("none"));
	Out.Version = IntOr(Json, TEXT("version"));

	double Points = 0.0;
	Out.bHasPoints = Json->TryGetNumberField(TEXT("points"), Points);
	Out.Points = Out.bHasPoints ? FMath::RoundToInt32(Points) : 0;

	Out.Labels.Reset();
	Json->TryGetStringArrayField(TEXT("labels"), Out.Labels);
	return true;
}

bool PurrJson::ParseProjectList(const FString& Body, TArray<FPurrProject>& Out)
{
	const TSharedPtr<FJsonObject> Root = ReadObject(Body);
	const TArray<TSharedPtr<FJsonValue>>* Items = nullptr;
	if (!Root.IsValid() || !Root->TryGetArrayField(TEXT("items"), Items))
	{
		return false;
	}
	Out.Reset();
	for (const TSharedPtr<FJsonValue>& Value : *Items)
	{
		FPurrProject Project;
		if (ParseProject(Value->AsObject(), Project))
		{
			Out.Add(MoveTemp(Project));
		}
	}
	return true;
}

bool PurrJson::ParseTaskPage(const FString& Body, TArray<FPurrTask>& Out, FString& OutCursor)
{
	const TSharedPtr<FJsonObject> Root = ReadObject(Body);
	const TArray<TSharedPtr<FJsonValue>>* Items = nullptr;
	if (!Root.IsValid() || !Root->TryGetArrayField(TEXT("items"), Items))
	{
		return false;
	}
	Out.Reset();
	for (const TSharedPtr<FJsonValue>& Value : *Items)
	{
		FPurrTask Task;
		if (ParseTask(Value->AsObject(), Task))
		{
			Out.Add(MoveTemp(Task));
		}
	}
	// "cursor" is absent or null on the last page.
	OutCursor = StringOr(Root, TEXT("cursor"));
	return true;
}

bool PurrJson::ParseTaskBody(const FString& Body, FPurrTask& Out)
{
	return ParseTask(ReadObject(Body), Out);
}

bool PurrJson::ParseError(const FString& Body, FString& OutCode, FString& OutMessage)
{
	const TSharedPtr<FJsonObject> Root = ReadObject(Body);
	const TSharedPtr<FJsonObject>* Error = nullptr;
	if (!Root.IsValid() || !Root->TryGetObjectField(TEXT("error"), Error))
	{
		return false;
	}
	OutCode = StringOr(*Error, TEXT("code"));
	OutMessage = StringOr(*Error, TEXT("message"));
	return true;
}
