#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <metahost.h>
#include <cstdio>
#include <cwchar>
#include <string>

namespace
{
    constexpr wchar_t PayloadFileName[] = L"TerrariaSplit.WorldGuard.Payload.dll";
    constexpr wchar_t ManagedTypeName[] = L"TerrariaSplit.WorldGuard.Payload.EntryPoint";
    constexpr wchar_t ManagedMethodName[] = L"Initialize";
    constexpr DWORD CommandMappingCapacity = 65536;

    void BuildObjectName(wchar_t* buffer, size_t length, const wchar_t* kind, DWORD processId)
    {
        swprintf_s(buffer, length, L"Local\\TerrariaSplit.WorldGuard.%s.%lu", kind, processId);
    }

    bool ReadCommand(DWORD processId, std::wstring& command)
    {
        wchar_t mappingName[128] = {};
        BuildObjectName(mappingName, _countof(mappingName), L"Command", processId);
        HANDLE mapping = OpenFileMappingW(FILE_MAP_READ, FALSE, mappingName);
        if (mapping == nullptr)
        {
            return false;
        }

        void* view = MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, CommandMappingCapacity);
        if (view == nullptr)
        {
            CloseHandle(mapping);
            return false;
        }

        DWORD byteLength = *static_cast<DWORD*>(view);
        bool valid = byteLength > 0 &&
            byteLength <= CommandMappingCapacity - sizeof(DWORD) - sizeof(wchar_t) &&
            byteLength % sizeof(wchar_t) == 0;
        if (valid)
        {
            const wchar_t* text = reinterpret_cast<const wchar_t*>(static_cast<const BYTE*>(view) + sizeof(DWORD));
            command.assign(text, byteLength / sizeof(wchar_t));
        }

        UnmapViewOfFile(view);
        CloseHandle(mapping);
        return valid;
    }

    void SignalResult(DWORD processId, HRESULT executeResult, DWORD managedResult)
    {
        wchar_t mappingName[128] = {};
        BuildObjectName(mappingName, _countof(mappingName), L"Result", processId);
        HANDLE mapping = OpenFileMappingW(FILE_MAP_WRITE, FALSE, mappingName);
        if (mapping != nullptr)
        {
            auto* values = static_cast<DWORD*>(MapViewOfFile(mapping, FILE_MAP_WRITE, 0, 0, sizeof(DWORD) * 2));
            if (values != nullptr)
            {
                values[0] = static_cast<DWORD>(executeResult);
                values[1] = managedResult;
                UnmapViewOfFile(values);
            }

            CloseHandle(mapping);
        }

        wchar_t eventName[128] = {};
        BuildObjectName(eventName, _countof(eventName), L"Completed", processId);
        HANDLE completed = OpenEventW(EVENT_MODIFY_STATE, FALSE, eventName);
        if (completed != nullptr)
        {
            SetEvent(completed);
            CloseHandle(completed);
        }
    }

    DWORD WINAPI RunGuard(void* parameter)
    {
        HMODULE module = static_cast<HMODULE>(parameter);
        DWORD processId = GetCurrentProcessId();
        HRESULT result = E_FAIL;
        DWORD managedResult = MAXDWORD;
        std::wstring command;

        wchar_t payloadPath[MAX_PATH] = {};
        DWORD pathLength = GetModuleFileNameW(module, payloadPath, _countof(payloadPath));
        if (pathLength != 0 && pathLength < _countof(payloadPath) && ReadCommand(processId, command))
        {
            wchar_t* separator = wcsrchr(payloadPath, L'\\');
            if (separator != nullptr)
            {
                *(separator + 1) = L'\0';
                if (wcscat_s(payloadPath, PayloadFileName) == 0)
                {
                    ICLRMetaHost* metaHost = nullptr;
                    ICLRRuntimeInfo* runtimeInfo = nullptr;
                    ICLRRuntimeHost* runtime = nullptr;
                    result = CLRCreateInstance(
                        CLSID_CLRMetaHost,
                        IID_ICLRMetaHost,
                        reinterpret_cast<void**>(&metaHost));
                    if (SUCCEEDED(result) && metaHost != nullptr)
                    {
                        result = metaHost->GetRuntime(
                            L"v4.0.30319",
                            IID_ICLRRuntimeInfo,
                            reinterpret_cast<void**>(&runtimeInfo));
                    }

                    BOOL isLoaded = FALSE;
                    if (SUCCEEDED(result) && runtimeInfo != nullptr)
                    {
                        result = runtimeInfo->IsLoaded(GetCurrentProcess(), &isLoaded);
                        if (SUCCEEDED(result) && !isLoaded)
                        {
                            result = HRESULT_FROM_WIN32(ERROR_NOT_READY);
                        }
                    }

                    if (SUCCEEDED(result) && runtimeInfo != nullptr)
                    {
                        result = runtimeInfo->GetInterface(
                            CLSID_CLRRuntimeHost,
                            IID_ICLRRuntimeHost,
                            reinterpret_cast<void**>(&runtime));
                    }

                    if (SUCCEEDED(result) && runtime != nullptr)
                    {
                        HRESULT startResult = runtime->Start();
                        if (SUCCEEDED(startResult))
                        {
                            result = runtime->ExecuteInDefaultAppDomain(
                                payloadPath,
                                ManagedTypeName,
                                ManagedMethodName,
                                command.c_str(),
                                &managedResult);
                        }
                        else
                        {
                            result = startResult;
                        }

                        runtime->Release();
                    }

                    if (runtimeInfo != nullptr)
                    {
                        runtimeInfo->Release();
                    }

                    if (metaHost != nullptr)
                    {
                        metaHost->Release();
                    }
                }
            }
        }

        SignalResult(processId, result, managedResult);
        FreeLibraryAndExitThread(module, 0);
    }
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, void*)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
        HANDLE thread = CreateThread(nullptr, 0, RunGuard, module, 0, nullptr);
        if (thread != nullptr)
        {
            CloseHandle(thread);
        }
    }

    return TRUE;
}
