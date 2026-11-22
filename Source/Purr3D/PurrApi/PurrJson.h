// Parsing Purr API responses. Pure functions with no HTTP, so the specs can feed them captured JSON.

#pragma once

#include "CoreMinimal.h"
#include "PurrTypes.h"

class FJsonObject;

namespace PurrJson
{
	/** "backlog" etc. to the enum. Unknown groups become Backlog, the safest place to park a task. */
	PURR3D_API EPurrStateGroup ParseStateGroup(const FString& Group);

	PURR3D_API bool ParseProject(const TSharedPtr<FJsonObject>& Json, FPurrProject& Out);
	PURR3D_API bool ParseTask(const TSharedPtr<FJsonObject>& Json, FPurrTask& Out);

	/** GET /projects body: {"items":[Project...]}. */
	PURR3D_API bool ParseProjectList(const FString& Body, TArray<FPurrProject>& Out);

	/** GET /projects/{prefix}/tasks body: {"items":[Task...], "cursor"?: string}. OutCursor is empty on the last page. */
	PURR3D_API bool ParseTaskPage(const FString& Body, TArray<FPurrTask>& Out, FString& OutCursor);

	/** A single Task body, as POST /tasks/{ref}/state returns. */
	PURR3D_API bool ParseTaskBody(const FString& Body, FPurrTask& Out);

	/** {"error":{"code","message"}}. Returns false when the body isn't a Purr error. */
	PURR3D_API bool ParseError(const FString& Body, FString& OutCode, FString& OutMessage);
}
