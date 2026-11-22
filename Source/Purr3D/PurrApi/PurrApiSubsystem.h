// The Purr REST API, for Blueprints. Lives as long as the game instance, so it survives level changes.
//
// Config:
//   Config/DefaultGame.ini   [/Script/Purr3D.PurrApiSubsystem] BaseUrl=https://purr.sandbox.nakomis.com/api
//   Config/PurrSecrets.ini   [/Script/Purr3D.PurrApiSubsystem] ApiKey=...    (gitignored)
//   or the PURR_API_KEY environment variable, which wins over the ini.

#pragma once

#include "CoreMinimal.h"
#include "Interfaces/IHttpRequest.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "PurrTypes.h"
#include "PurrApiSubsystem.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnPurrProjectsLoaded, const TArray<FPurrProject>&, Projects);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnPurrTasksLoaded, const FString&, Prefix, const TArray<FPurrTask>&, Tasks);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnPurrTaskUpdated, const FPurrTask&, Task);

/**
 * Something went wrong. Operation is "FetchProjects", "FetchTasks" or "SetTaskState".
 * Subject is the prefix or task ref it was about, so a Blueprint can roll back the right object.
 * Status is the HTTP status, or 0 when the request never got an answer.
 */
DECLARE_DYNAMIC_MULTICAST_DELEGATE_FourParams(FOnPurrError, const FString&, Operation, const FString&, Subject, int32, Status, const FString&, Message);

UCLASS(Config = Game)
class PURR3D_API UPurrApiSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;

	/** GET /projects. Archived projects are left out by the API. */
	UFUNCTION(BlueprintCallable, Category = "Purr")
	void FetchProjects();

	/** GET /projects/{prefix}/tasks?all=true, following cursors. OnTasksLoaded fires once, with every page. */
	UFUNCTION(BlueprintCallable, Category = "Purr")
	void FetchTasks(const FString& Prefix);

	/** POST /tasks/{ref}/state. Pass a state id (names work too, but ids never collide). */
	UFUNCTION(BlueprintCallable, Category = "Purr")
	void SetTaskState(const FString& Ref, const FString& StateId);

	/** True when an API key was found. Without one every call fails with 401. */
	UFUNCTION(BlueprintPure, Category = "Purr")
	bool HasApiKey() const { return !ApiKey.IsEmpty(); }

	UPROPERTY(BlueprintAssignable, Category = "Purr")
	FOnPurrProjectsLoaded OnProjectsLoaded;

	UPROPERTY(BlueprintAssignable, Category = "Purr")
	FOnPurrTasksLoaded OnTasksLoaded;

	UPROPERTY(BlueprintAssignable, Category = "Purr")
	FOnPurrTaskUpdated OnTaskUpdated;

	UPROPERTY(BlueprintAssignable, Category = "Purr")
	FOnPurrError OnError;

	/** e.g. https://purr.sandbox.nakomis.com/api (no trailing slash). From DefaultGame.ini. */
	UPROPERTY(Config, BlueprintReadOnly, Category = "Purr")
	FString BaseUrl = TEXT("https://purr.sandbox.nakomis.com/api");

private:
	/** Builds an authenticated request. Verb is GET or POST. */
	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> MakeRequest(const FString& Verb, const FString& Path) const;

	void FetchTaskPage(const FString& Prefix, const FString& Cursor, TSharedRef<TArray<FPurrTask>> Accumulated);

	/** Broadcasts OnError from a failed response, using Purr's error body when there is one. */
	void Fail(const FString& Operation, const FString& Subject, FHttpResponsePtr Response, bool bConnected);

	/** Never a UPROPERTY: keeps the key out of anything Unreal serialises. */
	FString ApiKey;

	/** Tasks per page. The API caps it; 100 keeps a big project to a handful of requests. */
	static constexpr int32 PageSize = 100;
};
