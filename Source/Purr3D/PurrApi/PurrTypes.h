// Purr's domain, as Blueprint-visible structs. Mirrors packages/domain/src/types.ts in nakomis/purr.

#pragma once

#include "CoreMinimal.h"
#include "PurrTypes.generated.h"

/** A state's group decides how shooting moves a task through it (see UPurrStateCycle). */
UENUM(BlueprintType)
enum class EPurrStateGroup : uint8
{
	Backlog,
	Unstarted,
	Started,
	Completed,
	Cancelled
};

USTRUCT(BlueprintType)
struct PURR3D_API FPurrState
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Id;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Name;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	EPurrStateGroup Group = EPurrStateGroup::Backlog;

	/** Purr's sort key. The API's array is NOT in this order: always sort. */
	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	int32 Order = 0;
};

USTRUCT(BlueprintType)
struct PURR3D_API FPurrProject
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Id;

	/** The ref prefix, e.g. "HOME". */
	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Prefix;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Name;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Description;

	/** Site-relative icon path, e.g. "/icons/HOME.png". May be empty. */
	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Icon;

	/** States sorted by Order (the parser sorts them). */
	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	TArray<FPurrState> States;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	bool bArchived = false;
};

USTRUCT(BlueprintType)
struct PURR3D_API FPurrTask
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Id;

	/** "HOME-42". */
	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Ref;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString ProjectId;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Title;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString StateId;

	/** none | low | medium | high | urgent */
	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	FString Priority;

	/** Only meaningful when bHasPoints: Purr's points are optional. */
	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	int32 Points = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	bool bHasPoints = false;

	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	TArray<FString> Labels;

	/** Optimistic-concurrency version; bumps on every change. */
	UPROPERTY(BlueprintReadOnly, Category = "Purr")
	int32 Version = 0;
};
