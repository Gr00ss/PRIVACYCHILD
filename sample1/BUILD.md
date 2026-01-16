# Windows Family Monitor - Build Instructions

## Требования

- Windows 10/11 (64-bit)
- .NET 8.0 SDK или выше
- Visual Studio 2022 (опционально) или VS Code
- PowerShell 5.1 или выше

## Сборка проекта

### Метод 1: Использование Visual Studio

1. Откройте `sample1.csproj` в Visual Studio 2022
2. Выберите конфигурацию **Release**
3. В меню выберите **Build → Publish sample1**
4. Выберите профиль публикации или создайте новый:
   - Target Runtime: **win-x64**
   - Deployment Mode: **Self-contained**
   - File publish options: ✓ **Produce single file**
5. Нажмите **Publish**

### Метод 2: Использование командной строки (рекомендуется)

1. Откройте PowerShell или Command Prompt
2. Перейдите в папку проекта:
   ```powershell
   cd c:\Users\ZOV\Documents\PRIVACYCHILD\sample1
   ```

3. Восстановите зависимости:
   ```powershell
   dotnet restore
   ```

4. Соберите проект:
   ```powershell
   dotnet build -c Release
   ```

5. Опубликуйте проект:
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true
   ```

### Где найти собранные файлы

После успешной сборки исполняемый файл будет находиться в:
```
sample1\bin\Release\net8.0-windows\win-x64\publish\sample1.exe
```

## Подготовка к установке

1. Скопируйте следующие файлы в отдельную папку:
   - `sample1.exe` (из папки publish)
   - `appsettings.json`
   - `install.ps1`
   - `uninstall.ps1`
   - `README.txt`

2. Эту папку можно передать для установки на целевой компьютер

## Установка

1. Откройте PowerShell от имени **Администратора**
2. Перейдите в папку с файлами
3. Разрешите выполнение скриптов (если требуется):
   ```powershell
   Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
   ```
4. Запустите установку:
   ```powershell
   .\install.ps1
   ```

## Проверка работы

### Проверка статуса сервиса

```powershell
Get-Service -Name "WindowsNetworkHealthService"
```

Статус должен быть **Running**.

### Просмотр логов

Логи находятся в:
```
C:\ProgramData\Microsoft\NetworkDiagnostics\Logs\
```

### Тестирование Telegram бота

1. Откройте Telegram
2. Найдите вашего бота
3. Отправьте `/start`
4. Попробуйте команды `/apps` и `/network`

## Удаление

```powershell
.\uninstall.ps1
```

## Устранение неполадок

### Ошибка "Требуется .NET Runtime"

Проект собран как self-contained, но если возникает ошибка:
- Переберите проект с параметром `--self-contained true`
- Или установите .NET 8.0 Runtime на целевой машине

### Сервис не запускается

1. Проверьте логи в `C:\ProgramData\Microsoft\NetworkDiagnostics\Logs\`
2. Убедитесь, что токен Telegram бота корректный
3. Проверьте права доступа к папке установки

### Бот не отвечает

1. Проверьте, что сервис запущен
2. Убедитесь, что Telegram User ID правильный
3. Проверьте подключение к интернету

## Конфигурация appsettings.json

Перед установкой можно изменить настройки в `appsettings.json`:

- **Telegram.BotToken**: Токен вашего Telegram бота
- **Telegram.AuthorizedUsers**: Список ID пользователей с доступом
- **Telegram.ReportTime**: Время отправки ежедневных отчетов
- **Monitoring.ProcessCheckIntervalMs**: Интервал проверки процессов (мс)
- **Monitoring.NetworkCheckIntervalMs**: Интервал проверки сети (мс)
- **Database.DataRetentionDays**: Сколько дней хранить данные

## Структура проекта

```
sample1/
├── Program.cs                      # Точка входа и DI контейнер
├── appsettings.json                # Конфигурация
├── sample1.csproj                  # Файл проекта
├── Models/
│   ├── ActivityRecord.cs           # Модель записи активности
│   └── AppConfig.cs                # Модели конфигурации
├── Services/
│   ├── IActivityMonitor.cs         # Интерфейс мониторинга
│   └── IDataService.cs             # Интерфейс данных
├── Data/
│   └── Database.cs                 # SQLite база данных
├── Monitoring/
│   ├── ProcessMonitor.cs           # Мониторинг процессов
│   └── NetworkMonitor.cs           # Мониторинг сети
├── Bot/
│   ├── TelegramBotService.cs       # Основной сервис бота
│   └── ReportGenerator.cs          # Генератор отчетов
├── Security/
│   ├── EncryptionService.cs        # Шифрование AES-256
│   ├── StealthService.cs           # Скрытие процесса
│   └── WatchdogService.cs          # Защита от остановки
├── install.ps1                     # Скрипт установки
├── uninstall.ps1                   # Скрипт удаления
└── README.txt                      # Инструкция для пользователя
```

## Зависимости

Все зависимости указаны в `sample1.csproj`:

- Microsoft.Extensions.Hosting (8.0.0)
- Microsoft.Extensions.Hosting.WindowsServices (8.0.0)
- Microsoft.Data.Sqlite (8.0.0)
- Telegram.Bot (21.0.0)
- Serilog.Extensions.Hosting (8.0.0)
- Serilog.Sinks.File (5.0.0)
- System.Management (8.0.0)

Все пакеты будут автоматически загружены при сборке.

## Безопасность

⚠️ **ВАЖНО**: 
- Никогда не делитесь токеном Telegram бота
- Храните файл `appsettings.json` в безопасности
- Регулярно обновляйте список авторизованных пользователей
- Проверяйте логи на подозрительную активность

## Производительность

Ожидаемое потребление ресурсов:
- CPU: < 2% в режиме idle
- RAM: < 50MB
- Диск: ~ 100MB (включая .NET runtime и базу данных)

## Лицензия

Этот проект предназначен только для семейного использования.
