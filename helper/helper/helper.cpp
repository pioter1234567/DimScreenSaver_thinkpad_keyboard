#include <winrt/base.h>
#include <winrt/Windows.System.Power.h>
#include <iostream>

using namespace winrt;
using namespace Windows::System::Power;

int main()
{
    init_apartment(); // wymagane dla C++/WinRT

    auto saverStatus = PowerManager::EnergySaverStatus();
    bool isSaverActive = (saverStatus == EnergySaverStatus::On);

    std::cout << "BatterySaverActive = "
        << (isSaverActive ? "true" : "false") << std::endl;

    return isSaverActive ? 1 : 0;
}