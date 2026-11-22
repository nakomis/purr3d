// Where a shot task goes next. Pure logic, unit-tested in Tests/PurrApiSpec.cpp.
//
// Real projects make "next by order" wrong: Triage is a backlog state sorted after Done,
// and one project lists Published (completed) first. So:
//   - any backlog state  -> the first state of the earliest later group (unstarted, else started, else completed)
//   - otherwise          -> the next state in (group, order) order, never into cancelled
//   - completed / cancelled tasks stay put

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "PurrTypes.h"
#include "PurrStateCycle.generated.h"

UCLASS()
class PURR3D_API UPurrStateCycle : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/** The state a shot moves CurrentStateId to. False when the task stays put (finished, or unknown state). */
	UFUNCTION(BlueprintPure, Category = "Purr", meta = (ReturnDisplayName = "Moves"))
	static bool NextState(const FPurrProject& Project, const FString& CurrentStateId, FPurrState& OutNext);

	/** Looks a state up by id. False if the project has no such state. */
	UFUNCTION(BlueprintPure, Category = "Purr", meta = (ReturnDisplayName = "Found"))
	static bool FindState(const FPurrProject& Project, const FString& StateId, FPurrState& OutState);

	/** Completed or cancelled: shooting does nothing. */
	UFUNCTION(BlueprintPure, Category = "Purr")
	static bool IsFinished(EPurrStateGroup Group);
};
