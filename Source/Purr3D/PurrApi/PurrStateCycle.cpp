#include "PurrStateCycle.h"

namespace
{
	/** Progress rank of a group. Cancelled is off the path entirely. */
	int32 GroupRank(EPurrStateGroup Group)
	{
		switch (Group)
		{
		case EPurrStateGroup::Backlog: return 0;
		case EPurrStateGroup::Unstarted: return 1;
		case EPurrStateGroup::Started: return 2;
		case EPurrStateGroup::Completed: return 3;
		default: return INDEX_NONE;
		}
	}
}

bool UPurrStateCycle::IsFinished(EPurrStateGroup Group)
{
	return Group == EPurrStateGroup::Completed || Group == EPurrStateGroup::Cancelled;
}

bool UPurrStateCycle::FindState(const FPurrProject& Project, const FString& StateId, FPurrState& OutState)
{
	const FPurrState* Found = Project.States.FindByPredicate([&StateId](const FPurrState& S) { return S.Id == StateId; });
	if (!Found)
	{
		return false;
	}
	OutState = *Found;
	return true;
}

bool UPurrStateCycle::NextState(const FPurrProject& Project, const FString& CurrentStateId, FPurrState& OutNext)
{
	FPurrState Current;
	if (!FindState(Project, CurrentStateId, Current) || IsFinished(Current.Group))
	{
		return false;
	}

	// The path: every non-cancelled state, by group then order.
	TArray<FPurrState> Path = Project.States.FilterByPredicate([](const FPurrState& S) { return S.Group != EPurrStateGroup::Cancelled; });
	Path.StableSort([](const FPurrState& A, const FPurrState& B)
	{
		const int32 RankA = GroupRank(A.Group);
		const int32 RankB = GroupRank(B.Group);
		return RankA != RankB ? RankA < RankB : A.Order < B.Order;
	});

	const FPurrState* Next = nullptr;
	if (Current.Group == EPurrStateGroup::Backlog)
	{
		// Every backlog state means "not yet": leave the group altogether.
		Next = Path.FindByPredicate([](const FPurrState& S) { return S.Group != EPurrStateGroup::Backlog; });
	}
	else
	{
		const int32 Index = Path.IndexOfByPredicate([&CurrentStateId](const FPurrState& S) { return S.Id == CurrentStateId; });
		if (Path.IsValidIndex(Index + 1))
		{
			Next = &Path[Index + 1];
		}
	}

	if (!Next)
	{
		return false;
	}
	OutNext = *Next;
	return true;
}
