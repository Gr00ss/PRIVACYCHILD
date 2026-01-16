# Примеры использования Windows Family Monitor

## 📱 Примеры команд Telegram бота

### Получение отчета по приложениям

**Команда:**
```
/apps
```

**Пример ответа:**
```
📱 Отчет по приложениям за 16.01

• chrome: 3ч 25м
• code: 2ч 15м
• discord: 1ч 45м
• spotify: 45м

Всего: 8ч 10м
```

### Получение отчета по сайтам

**Команда:**
```
/network
```

**Пример ответа:**
```
🌐 Отчет по сайтам за 16.01

• youtube.com: 2ч 30м
• github.com: 1ч 20м
• reddit.com: 1ч 5м
• stackoverflow.com: 45м

Всего: 5ч 40м
```

### Установка времени отчетов

**Команда:**
```
/settime 20:30
```

**Ответ:**
```
✅ Время ежедневных отчетов установлено на 20:30
```

### Главное меню

**Команда:**
```
/start
```

**Ответ:**
Приветственное сообщение с кнопками:
- 📱 Приложения
- 🌐 Сеть

## 🔧 Примеры администрирования

### Проверка статуса сервиса

```powershell
Get-Service -Name "WindowsNetworkHealthService"
```

**Вывод:**
```
Status   Name                           DisplayName
------   ----                           -----------
Running  WindowsNetworkHealthService    Windows Network Health Service
```

### Просмотр последних логов

```powershell
Get-Content "C:\ProgramData\Microsoft\NetworkDiagnostics\Logs\service*.log" -Tail 50
```

### Перезапуск сервиса

```powershell
Restart-Service -Name "WindowsNetworkHealthService"
```

### Остановка сервиса

```powershell
Stop-Service -Name "WindowsNetworkHealthService"
```

### Запуск сервиса

```powershell
Start-Service -Name "WindowsNetworkHealthService"
```

## 📝 Примеры конфигурации

### Добавление второго авторизованного пользователя

Отредактируйте `appsettings.json`:

```json
{
  "Telegram": {
    "BotToken": "ваш_токен",
    "AuthorizedUsers": [123456789, 987654321],
    "ReportTime": "19:00"
  }
}
```

После изменения перезапустите сервис:
```powershell
Restart-Service -Name "WindowsNetworkHealthService"
```

### Изменение интервалов мониторинга

```json
{
  "Monitoring": {
    "ProcessCheckIntervalMs": 2000,
    "NetworkCheckIntervalMs": 10000,
    "DataSaveIntervalMs": 600000
  }
}
```

- `ProcessCheckIntervalMs`: Как часто проверять активное окно (в мс)
- `NetworkCheckIntervalMs`: Как часто проверять DNS кэш (в мс)
- `DataSaveIntervalMs`: Как часто сохранять данные в БД (в мс)

### Добавление системных процессов для игнорирования

```json
{
  "SystemProcesses": [
    "explorer",
    "svchost",
    "notepad",
    "cmd"
  ]
}
```

### Добавление системных доменов для игнорирования

```json
{
  "SystemDomains": [
    "microsoft.com",
    "windowsupdate.com",
    "cloudflare.com"
  ]
}
```

## 📊 Примеры работы с базой данных

### Подключение к базе данных SQLite

```powershell
# Установите SQLite
# Затем подключитесь к БД
sqlite3 "C:\ProgramData\Microsoft\NetworkDiagnostics\activity.db"
```

### Просмотр приложений за сегодня

```sql
SELECT 
    a.Name,
    SUM(ds.Seconds) / 3600 as Hours,
    (SUM(ds.Seconds) % 3600) / 60 as Minutes
FROM DailyStats ds
JOIN Applications a ON ds.AppId = a.Id
WHERE ds.Date = date('now')
GROUP BY a.Name
ORDER BY SUM(ds.Seconds) DESC;
```

### Просмотр топ-5 сайтов за неделю

```sql
SELECT 
    d.Name,
    SUM(ds.Seconds) / 3600 as Hours
FROM DailyStats ds
JOIN Domains d ON ds.DomainId = d.Id
WHERE ds.Date >= date('now', '-7 days')
GROUP BY d.Name
ORDER BY SUM(ds.Seconds) DESC
LIMIT 5;
```

### Очистка старых данных вручную

```sql
DELETE FROM DailyStats 
WHERE Date < date('now', '-7 days');
```

## 🛠️ Примеры устранения неполадок

### Проблема: Бот не отвечает

**Диагностика:**
```powershell
# 1. Проверить статус сервиса
Get-Service -Name "WindowsNetworkHealthService"

# 2. Проверить логи
Get-Content "C:\ProgramData\Microsoft\NetworkDiagnostics\Logs\service*.log" | Select-String "error" -Context 2

# 3. Проверить конфигурацию
Get-Content "C:\ProgramData\Microsoft\NetworkDiagnostics\appsettings.json"
```

**Решение:**
```powershell
# Перезапустить сервис
Restart-Service -Name "WindowsNetworkHealthService"
```

### Проблема: Высокое потребление ресурсов

**Диагностика:**
```powershell
Get-Process | Where-Object {$_.ProcessName -like "*svchost*"} | Select-Object ProcessName, CPU, WorkingSet64
```

**Решение:** Увеличить интервалы в `appsettings.json`:
```json
{
  "Monitoring": {
    "ProcessCheckIntervalMs": 3000,
    "NetworkCheckIntervalMs": 15000
  }
}
```

### Проблема: База данных слишком большая

**Диагностика:**
```powershell
Get-Item "C:\ProgramData\Microsoft\NetworkDiagnostics\activity.db" | Select-Object Length
```

**Решение:** Уменьшить срок хранения данных:
```json
{
  "Database": {
    "DataRetentionDays": 3
  }
}
```

## 📈 Примеры мониторинга

### Отслеживание производительности сервиса

```powershell
# Скрипт для мониторинга
$serviceName = "WindowsNetworkHealthService"
$process = Get-Process | Where-Object {$_.ProcessName -eq "svchost_net"}

if ($process) {
    Write-Host "CPU: $($process.CPU)%"
    Write-Host "RAM: $([math]::Round($process.WorkingSet64/1MB, 2)) MB"
} else {
    Write-Host "Service process not found"
}
```

### Автоматическая проверка здоровья

```powershell
# Создайте scheduled task для ежедневной проверки
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-File C:\Scripts\check-monitor.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At 9am
Register-ScheduledTask -TaskName "CheckFamilyMonitor" -Action $action -Trigger $trigger
```

## 🎯 Сценарии использования

### Сценарий 1: Мониторинг времени игр

1. Получайте ежедневный отчет в 20:00
2. Проверяйте время в Steam, Epic Games, etc.
3. Анализируйте тренды за неделю

### Сценарий 2: Контроль учебного времени

1. Проверяйте использование образовательных приложений
2. Сравнивайте с развлекательными приложениями
3. Отправляйте еженедельные сводки

### Сценарий 3: Мониторинг продуктивности

1. Отслеживайте время в IDE (VS Code, Visual Studio)
2. Мониторьте посещение профессиональных сайтов
3. Анализируйте баланс работа/отдых

## 🔐 Примеры безопасности

### Шифрование токена бота (автоматически)

Сервис автоматически шифрует токен при установке. Проверка:

```powershell
Get-Content "C:\ProgramData\Microsoft\NetworkDiagnostics\appsettings.json" | ConvertFrom-Json | Select-Object -ExpandProperty Security
```

### Ограничение доступа к файлам

```powershell
# Только для администраторов
$path = "C:\ProgramData\Microsoft\NetworkDiagnostics"
$acl = Get-Acl $path
$acl.SetAccessRuleProtection($true, $false)
Set-Acl $path $acl
```

### Аудит доступа к боту

Проверьте логи на несанкционированные попытки доступа:

```powershell
Get-Content "C:\ProgramData\Microsoft\NetworkDiagnostics\Logs\service*.log" | Select-String "Доступ запрещен"
```

---

## 📞 Дополнительная помощь

Для получения дополнительной помощи:
1. Читайте `README.txt` - полная инструкция
2. Смотрите `BUILD.md` - детали сборки
3. Проверяйте логи сервиса

**Telegram бот работает!** Наслаждайтесь мониторингом! 🎉
