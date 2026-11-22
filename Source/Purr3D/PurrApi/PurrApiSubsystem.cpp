#include "PurrApiSubsystem.h"

#include "GenericPlatform/GenericPlatformHttp.h"
#include "HAL/PlatformMisc.h"
#include "HttpModule.h"
#include "Interfaces/IHttpResponse.h"
#include "Misc/ConfigCacheIni.h"
#include "Misc/Paths.h"
#include "PurrJson.h"
#include "Serialization/JsonWriter.h"

DEFINE_LOG_CATEGORY_STATIC(LogPurrApi, Log, All);

namespace
{
	const TCHAR* ConfigSection = TEXT("/Script/Purr3D.PurrApiSubsystem");

	bool IsSuccess(FHttpResponsePtr Response)
	{
		return Response.IsValid() && EHttpResponseCodes::IsOk(Response->GetResponseCode());
	}
}

void UPurrApiSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);

	BaseUrl.RemoveFromEnd(TEXT("/"));

	ApiKey = FPlatformMisc::GetEnvironmentVariable(TEXT("PURR_API_KEY"));
	if (ApiKey.IsEmpty())
	{
		const FString SecretsPath = FPaths::Combine(FPaths::ProjectConfigDir(), TEXT("PurrSecrets.ini"));
		FConfigFile Secrets;
		Secrets.Read(SecretsPath);
		Secrets.GetString(ConfigSection, TEXT("ApiKey"), ApiKey);
	}
	ApiKey.TrimStartAndEndInline();

	// Never log the key itself.
	UE_LOG(LogPurrApi, Log, TEXT("Purr API at %s; API key %s"), *BaseUrl, ApiKey.IsEmpty() ? TEXT("MISSING") : TEXT("found"));
}

TSharedRef<IHttpRequest, ESPMode::ThreadSafe> UPurrApiSubsystem::MakeRequest(const FString& Verb, const FString& Path) const
{
	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
	Request->SetVerb(Verb);
	Request->SetURL(BaseUrl + Path);
	Request->SetHeader(TEXT("Accept"), TEXT("application/json"));
	Request->SetHeader(TEXT("x-api-key"), ApiKey);
	Request->SetTimeout(15.0f);
	return Request;
}

void UPurrApiSubsystem::FetchProjects()
{
	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = MakeRequest(TEXT("GET"), TEXT("/projects"));
	TWeakObjectPtr<UPurrApiSubsystem> WeakThis(this);
	Request->OnProcessRequestComplete().BindLambda([WeakThis](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnected)
	{
		UPurrApiSubsystem* Self = WeakThis.Get();
		if (!Self) return;
		TArray<FPurrProject> Projects;
		if (!bConnected || !IsSuccess(Response) || !PurrJson::ParseProjectList(Response->GetContentAsString(), Projects))
		{
			Self->Fail(TEXT("FetchProjects"), FString(), Response, bConnected);
			return;
		}
		// The API already leaves archived projects out; this is belt and braces.
		Projects.RemoveAll([](const FPurrProject& P) { return P.bArchived; });
		Self->OnProjectsLoaded.Broadcast(Projects);
	});
	Request->ProcessRequest();
}

void UPurrApiSubsystem::FetchTasks(const FString& Prefix)
{
	FetchTaskPage(Prefix, FString(), MakeShared<TArray<FPurrTask>>());
}

void UPurrApiSubsystem::FetchTaskPage(const FString& Prefix, const FString& Cursor, TSharedRef<TArray<FPurrTask>> Accumulated)
{
	FString Path = FString::Printf(TEXT("/projects/%s/tasks?all=true&limit=%d"), *FGenericPlatformHttp::UrlEncode(Prefix), PageSize);
	if (!Cursor.IsEmpty())
	{
		Path += TEXT("&cursor=") + FGenericPlatformHttp::UrlEncode(Cursor);
	}

	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = MakeRequest(TEXT("GET"), Path);
	TWeakObjectPtr<UPurrApiSubsystem> WeakThis(this);
	Request->OnProcessRequestComplete().BindLambda([WeakThis, Prefix, Accumulated](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnected)
	{
		UPurrApiSubsystem* Self = WeakThis.Get();
		if (!Self) return;
		TArray<FPurrTask> Page;
		FString NextCursor;
		if (!bConnected || !IsSuccess(Response) || !PurrJson::ParseTaskPage(Response->GetContentAsString(), Page, NextCursor))
		{
			Self->Fail(TEXT("FetchTasks"), Prefix, Response, bConnected);
			return;
		}
		Accumulated->Append(MoveTemp(Page));
		if (NextCursor.IsEmpty())
		{
			Self->OnTasksLoaded.Broadcast(Prefix, *Accumulated);
		}
		else
		{
			Self->FetchTaskPage(Prefix, NextCursor, Accumulated);
		}
	});
	Request->ProcessRequest();
}

void UPurrApiSubsystem::SetTaskState(const FString& Ref, const FString& StateId)
{
	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request =
		MakeRequest(TEXT("POST"), FString::Printf(TEXT("/tasks/%s/state"), *FGenericPlatformHttp::UrlEncode(Ref)));
	Request->SetHeader(TEXT("Content-Type"), TEXT("application/json"));

	// Built with the JSON writer rather than Printf so a state id can never break the body.
	FString Body;
	{
		TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Body);
		Writer->WriteObjectStart();
		Writer->WriteValue(TEXT("state"), StateId);
		Writer->WriteObjectEnd();
		Writer->Close();
	}
	Request->SetContentAsString(Body);

	TWeakObjectPtr<UPurrApiSubsystem> WeakThis(this);
	Request->OnProcessRequestComplete().BindLambda([WeakThis, Ref](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnected)
	{
		UPurrApiSubsystem* Self = WeakThis.Get();
		if (!Self) return;
		FPurrTask Task;
		if (!bConnected || !IsSuccess(Response) || !PurrJson::ParseTaskBody(Response->GetContentAsString(), Task))
		{
			Self->Fail(TEXT("SetTaskState"), Ref, Response, bConnected);
			return;
		}
		Self->OnTaskUpdated.Broadcast(Task);
	});
	Request->ProcessRequest();
}

void UPurrApiSubsystem::Fail(const FString& Operation, const FString& Subject, FHttpResponsePtr Response, bool bConnected)
{
	const int32 Status = (bConnected && Response.IsValid()) ? Response->GetResponseCode() : 0;
	FString Message = bConnected ? TEXT("unexpected response") : TEXT("no response (network or timeout)");
	if (Response.IsValid())
	{
		FString Code, ErrorMessage;
		if (PurrJson::ParseError(Response->GetContentAsString(), Code, ErrorMessage))
		{
			Message = FString::Printf(TEXT("%s: %s"), *Code, *ErrorMessage);
		}
	}
	UE_LOG(LogPurrApi, Warning, TEXT("%s %s failed (%d): %s"), *Operation, *Subject, Status, *Message);
	OnError.Broadcast(Operation, Subject, Status, Message);
}
