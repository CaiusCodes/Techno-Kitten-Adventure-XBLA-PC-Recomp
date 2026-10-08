#include <windows.h>
#include <filesystem>
#include <string>
#include <vector>

// Small console-free entry point. Runtime and conversion tools stay together
// under resources; nothing is extracted to a global runtime cache.
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR arguments, int) {
    std::vector<wchar_t> location(32768);
    const DWORD count = GetModuleFileNameW(nullptr, location.data(), DWORD(location.size()));
    if (count == 0 || count >= location.size()) return 1;
    const auto root = std::filesystem::path(location.data()).parent_path();
    const auto child = root / L"resources" / L"installer" / L"Setup Techno Kitten Adventure.exe";
    if (!std::filesystem::is_regular_file(child)) {
        MessageBoxW(nullptr, L"Please extract the complete installer download, including its resources folder.", L"Techno Kitten Adventure Setup", MB_OK | MB_ICONERROR);
        return 1;
    }
    std::wstring command = L"\"" + child.wstring() + L"\" --root \"" + root.wstring() + L"\"";
    if (arguments && *arguments) command += L" " + std::wstring(arguments);
    std::vector<wchar_t> buffer(command.begin(), command.end()); buffer.push_back(0);
    STARTUPINFOW startup{}; startup.cb = sizeof(startup);
    startup.dwFlags = STARTF_USESHOWWINDOW; startup.wShowWindow = SW_SHOWNORMAL;
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(child.c_str(), buffer.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, root.c_str(), &startup, &process)) {
        MessageBoxW(nullptr, L"Setup could not start. Check that the resources folder is beside this installer.", L"Techno Kitten Adventure Setup", MB_OK | MB_ICONERROR);
        return 1;
    }
    WaitForSingleObject(process.hProcess, INFINITE);
    DWORD status = 1; GetExitCodeProcess(process.hProcess, &status);
    CloseHandle(process.hThread); CloseHandle(process.hProcess);
    return int(status);
}
