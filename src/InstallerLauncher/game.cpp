#include <windows.h>
#include <shellapi.h>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

// No hashing/extraction or managed GUI startup on the normal launch path.
static bool file(const std::filesystem::path& path) {
    DWORD a = GetFileAttributesW(path.c_str());
    return a != INVALID_FILE_ATTRIBUTES && !(a & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT));
}
static bool ready(const std::filesystem::path& resources) {
    for (const auto* name : {L"game/Techno Kitten Adventure.exe", L"game/Techno Kitten Adventure.dll", L"game/MonoGame.Framework.dll", L"game/coreclr.dll", L"data/Helicopter.dll", L"data/tka-install.json", L"data/Content/font.xnb", L"data/Content/Fonts/MenuFont.xnb", L"data/Content/Graphics/cats.xnb", L"data/Content/Effects/effect0.xnb", L"data/Content/Effects/effect1.xnb", L"data/Content/Effects/effect2.xnb", L"data/Content/Effects/effect3.xnb", L"data/Content/Effects/effect4.xnb", L"data/Content/Music/newXactProject.xgs", L"data/Content/Music/Sound Bank.xsb", L"data/Content/Music/Wave Bank.xwb", L"data/Content/Music/MenuSong.wma"})
        if (!file(resources / name)) return false;
    std::ifstream stream(resources / L"data/tka-install.json");
    std::string marker(65537,'\0'); stream.read(marker.data(),marker.size()); marker.resize(size_t(stream.gcount()));
    return marker.size() < 65536 && marker.find("A472E517EF37A35A5C956C6ED558FE918F8D4E0C775995188CC17671FBB50EE5") != std::string::npos;
}
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR arguments, int) {
    std::vector<wchar_t> location(32768);
    DWORD count = GetModuleFileNameW(nullptr, location.data(), DWORD(location.size()));
    if (!count || count >= location.size()) return 1;
    const auto root = std::filesystem::path(location.data()).parent_path();
    const auto resources = root / L"resources";
    const std::wstring args = arguments ? arguments : L"";
    bool service = !ready(resources);
    const auto first = args.substr(0,args.find(L' '));
    if (first == L"--setup" || first == L"--migrate" || first.rfind(L"--install",0) == 0 || first.rfind(L"--preview",0) == 0 || first.rfind(L"--test",0) == 0 || first.rfind(L"--ui",0) == 0) service = true;
    const auto child = resources / (service ? L"setup/PortServices.exe" : L"game/Techno Kitten Adventure.exe");
    if (!file(child)) {
        MessageBoxW(nullptr,L"Please extract the complete download, including resources, into a writable folder.",L"Techno Kitten Adventure",MB_OK|MB_ICONERROR);
        return 1;
    }
    std::wstring command = L"\"" + child.wstring() + L"\"";
    if (service) command += L" --root \"" + root.wstring() + L"\" " + args;
    else {
        const auto data = resources / L"data";
        command += L" --game-root \"" + data.wstring() + L"\" \"" + (data / L"Helicopter.dll").wstring() + L"\" \"" + (data / L"Content").wstring() + L"\" ";
        command += first == L"--game-test" ? args.substr(12) : L"0 --fixed60 " + args;
    }
    std::vector<wchar_t> buffer(command.begin(),command.end()); buffer.push_back(0);
    STARTUPINFOW startup{}; startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    auto cwd = service ? root : resources / L"data";
    if (!CreateProcessW(child.c_str(),buffer.data(),nullptr,nullptr,FALSE,CREATE_NO_WINDOW,nullptr,cwd.c_str(),&startup,&process)) {
        MessageBoxW(nullptr,L"The game could not start. Extract the complete portable download again; keep resources/data to preserve your saves.",L"Techno Kitten Adventure",MB_OK|MB_ICONERROR);
        return 1;
    }
    WaitForSingleObject(process.hProcess,INFINITE);
    DWORD status = 1; GetExitCodeProcess(process.hProcess,&status);
    CloseHandle(process.hThread); CloseHandle(process.hProcess);
    return int(status);
}
