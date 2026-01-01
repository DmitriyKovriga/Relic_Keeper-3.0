Для работы билда нужно установить:
1) Unity 6000.0.62f1 (LTS)
2) Cinemachine
3) Localization

Плагины для VSCode
1) C#
2) C# Dev Kit
3) GitHub Copilot Chat
4) Russian Language Pack for Visual Studio Code
5) Unity
6) Unity Code Snippets
7) .NET Install Tool

Скачать:
VSCode
dontnet-sdk-9 https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-9.0.308-windows-x64-installer



Инструкции:
1) Как добавить кнопку к системе управления. Нужно добавить Row и в названии верно указать название импута в InputSystem, кроме того, 
обязательно зайти на компонент ControlsUi и в OnEnable добавить по аналогии с SetupRebind("Interact");
2) Как добавлять новые окна в UI, базовая структура:
WindowRoot (id: "WindowRoot")     ← скрывается/показывается
    ├── Overlay       (id: "Overlay", закрывает окно по клику)
    └── WindowPanel   (id: "WindowPanel", сама панель)
Нужно это для того чтобы в один скрипт навесить функциональность по сокрытию на ескейп и оверлею. Чтобы это окно не мешало другим и перекрывало их.