# Быстрая шпаргалка

## Создать релиз

```powershell
# 1. Убедитесь всё закоммичено
git status

# 2. Создайте тег
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0

# 3. Ждите 2-3 минуты → GitHub автоматически создаст Release
```

## Проверить сборку локально

```powershell
# Сборка
dotnet build -c Release

# Публикация
dotnet publish -c Release -r win-x64 --self-contained

# Проверка размера
Get-Item "bin\Release\net8.0-windows\win-x64\publish\sample1.exe" | Select Length
```

## Установка локально

```powershell
# От администратора
.\install.ps1

# Проверка
Get-ScheduledTask "WindowsNetworkHealthMonitor"
Get-Process NetworkHealthMonitor
```

## Удаление

```powershell
# От администратора
.\uninstall.ps1

# При запросе ответить: yes
```

## GitHub Actions статусы

```
GitHub → Actions → Workflows
```

- **Build and Test** - каждый push
- **Create Release** - при создании тега v*.*.*
- **CodeQL** - еженедельно + при PR

## Обновить README badges

Замените `YOURUSERNAME` на свой GitHub username:

```markdown
[![Build](https://github.com/YOURUSERNAME/PRIVACYCHILD/actions/workflows/build.yml/badge.svg)]
```

## Структура файлов

```
PRIVACYCHILD/
├── .github/
│   ├── workflows/              # CI/CD
│   │   ├── build.yml          # Сборка при push
│   │   ├── release.yml        # Релиз при тегах
│   │   └── codeql.yml         # Анализ безопасности
│   ├── ISSUE_TEMPLATE/        # Шаблоны Issues
│   └── WORKFLOWS_GUIDE.md     # Полная документация
├── Bot/                        # Telegram бот
├── Data/                       # База данных
├── Models/                     # Модели
├── Monitoring/                 # Мониторинг
├── Security/                   # Шифрование
├── Services/                   # Сервисы
├── README.md                   # Главная документация
├── CONTRIBUTING.md             # Гайд для контрибьюторов
├── SECURITY.md                 # Политика безопасности
├── LICENSE                     # MIT License
├── install.ps1                 # Установка
├── uninstall.ps1              # Удаление
└── sample1.csproj             # Проект
```

## Важные команды Git

```powershell
# Статус
git status

# Добавить все файлы
git add .

# Коммит
git commit -m "Описание изменений"

# Отправить в GitHub
git push origin main

# Создать тег для релиза
git tag -a v1.0.0 -m "Release description"
git push origin v1.0.0

# Посмотреть теги
git tag -l

# Удалить тег (если ошибка)
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

## Telegram команды

После установки:

- `/start` - Запустить бота
- `/report` - Получить отчёт за сегодня
- `/stats` - Статистика за 7 дней
- `/status` - Статус системы
- `/help` - Помощь

## Логи и отладка

```powershell
# Логи приложения
Get-Content "C:\ProgramData\Microsoft\NetworkDiagnostics\Logs\service*.log" -Tail 50

# Проверка задачи
Get-ScheduledTask "WindowsNetworkHealthMonitor" | Get-ScheduledTaskInfo

# Проверка процесса
Get-Process NetworkHealthMonitor | Format-List *

# База данных
$db = "C:\ProgramData\Microsoft\NetworkDiagnostics\activity.db"
if (Test-Path $db) {
    Get-Item $db | Select Name, Length, LastWriteTime
}
```

## Полезные ссылки

- **GitHub Actions**: `.github/WORKFLOWS_GUIDE.md`
- **Участие в разработке**: `CONTRIBUTING.md`
- **Безопасность**: `SECURITY.md`
- **Главная документация**: `README.md`

---

Храните эту шпаргалку под рукой для быстрого доступа! 📌
