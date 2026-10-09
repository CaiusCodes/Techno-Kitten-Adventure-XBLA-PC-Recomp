#include <windows.h>
#include <filesystem>
#include <string>
#include <vector>

// Keep the play EXE in Game and the managed/native runtime in Game/resources/game.
// Resolve from this EXE, never the caller's cwd.
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR arguments, int) {
    std::vector<wchar_t> location(32768);
    const DWORD count = GetModuleFileNameW(nullptr, location.data(), DWORD(location.size()));
    if (count == 0 || count >= location.size()) return 1;
    const auto root = std::filesystem::path(location.data()).parent_path();
    const auto runtime = root / L"resources" / L"game";
    const auto child = runtime / L"Techno Kitten Adventure.exe";
    if (GetFileAttributesW(child.c_str()) == INVALID_FILE_ATTRIBUTES) {
        MessageBoxW(nullptr, L"Run Setup Techno Kitten Adventure.exe first and choose your game package.", L"Techno Kitten Adventure!", MB_OK | MB_ICONINFORMATION);
        return 1;
    }
    std::wstring command = L"\"" + child.wstring() + L"\" --game-root \"" + root.wstring() + L"\"";
    if (arguments && *arguments) command += L" " + std::wstring(arguments);
    else command += L" \"" + (root / L"Helicopter.dll").wstring() + L"\" \"" +
        (root / L"Content").wstring() + L"\" 0 --fixed60";
    std::vector<wchar_t> buffer(command.begin(), command.end()); buffer.push_back(0);
    STARTUPINFOW startup{}; startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    // XNA TitleContainer resolves audio bank paths from the process cwd.
    if (!CreateProcessW(child.c_str(), buffer.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, root.c_str(), &startup, &process)) {
        MessageBoxW(nullptr, L"The game could not start. Run Setup again to repair the installation.", L"Techno Kitten Adventure!", MB_OK | MB_ICONERROR);
        return 1;
    }
    WaitForSingleObject(process.hProcess, INFINITE);
    DWORD status = 1; GetExitCodeProcess(process.hProcess, &status);
    CloseHandle(process.hThread); CloseHandle(process.hProcess);
    return int(status);
}
