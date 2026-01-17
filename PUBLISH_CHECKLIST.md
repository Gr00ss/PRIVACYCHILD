# Чеклист публикации на GitHub

## Перед первым push

### 1. Проверка безопасности
- [x] `appsettings.json` очищен от токенов
- [x] `appsettings.json` добавлен в `.gitignore`
- [x] Проверено отсутствие секретов в коде
- [ ] Запущен поиск секретов: `git secrets --scan` (если установлен)

### 2. Локальная проверка
- [ ] Проект собирается: `dotnet build -c Release`
- [ ] Публикация работает: `dotnet publish -c Release`
- [ ] Установка работает: `.\install.ps1`
- [ ] Удаление работает: `.\uninstall.ps1`

### 3. Git подготовка
```powershell
# Проверить статус
git status

# Должны быть только нужные файлы
git add .
git commit -m "Initial commit: Complete monitoring solution with CI/CD"

# Проверить что не добавлены лишние файлы
git log --stat
```

## Создание GitHub репозитория

### 1. На GitHub.com
- [ ] Создать новый репозиторий "PRIVACYCHILD"
- [ ] Описание: "Windows activity monitor with Telegram bot integration"
- [ ] Visibility: Public или Private (по выбору)
- [ ] НЕ создавать README, .gitignore, LICENSE (уже есть)

### 2. Первый push
```powershell
# Подключить remote (замените YOURUSERNAME)
git remote add origin https://github.com/YOURUSERNAME/PRIVACYCHILD.git

# Переименовать ветку в main (если нужно)
git branch -M main

# Отправить код
git push -u origin main
```

### 3. Проверка после push
- [ ] GitHub → Code: все файлы загружены
- [ ] GitHub → Actions: build.yml запустился
- [ ] Проверить логи сборки
- [ ] Убедиться что сборка прошла успешно ✓

## Обновление документации

### 1. Обновить badges в README.md
Заменить `YOURUSERNAME` на ваш GitHub username:

```powershell
# Открыть README.md
# Найти строки с badges (начало файла)
# Заменить YOURUSERNAME → ваш_username

git add README.md
git commit -m "Update: GitHub badges with correct username"
git push origin main
```

### 2. Проверить ссылки
- [ ] Все badges работают
- [ ] Ссылки на workflows корректны
- [ ] Issue templates доступны

## Настройка репозитория

### Settings → General
- [ ] Description: "Windows activity monitor with Telegram bot integration"
- [ ] Website: (URL документации если есть)
- [ ] Topics: 
  - `windows`
  - `monitoring`
  - `telegram-bot`
  - `csharp`
  - `dotnet`
  - `privacy`
  - `activity-tracker`

### Settings → Features
- [x] Issues
- [x] Discussions (опционально)
- [ ] Projects (опционально)
- [x] Wiki (опционально)

### Settings → Security
- [ ] Enable "Dependency graph"
- [ ] Enable "Dependabot alerts"
- [ ] Enable "Dependabot security updates"
- [ ] Code scanning: CodeQL уже настроен в workflow

### Settings → Actions → General
- [ ] Workflow permissions: "Read and write permissions"
- [ ] Allow GitHub Actions to create pull requests: ✓

## Создание первого релиза

### 1. Подготовка
```powershell
# Убедиться что всё закоммичено
git status

# Проверить что основная ветка актуальна
git pull origin main
```

### 2. Создание тега
```powershell
# Создать тег v1.0.0
git tag -a v1.0.0 -m "First stable release

Features:
- Process monitoring with Win32 API
- Network traffic tracking
- Telegram bot integration
- Daily automated reports
- Stealth operation mode
- Encrypted database
- Task Scheduler installation
- Complete PowerShell install/uninstall scripts

System Requirements:
- Windows 10/11 (64-bit)
- Administrator privileges for installation
"

# Отправить тег
git push origin v1.0.0
```

### 3. Проверка релиза
- [ ] GitHub → Actions: release.yml запустился
- [ ] Ожидание 2-3 минуты
- [ ] GitHub → Releases: появился Release v1.0.0
- [ ] Файлы в релизе:
  - [ ] Release.zip (~51-52 MB)
  - [ ] Release.zip.sha256
- [ ] Release Notes сгенерированы
- [ ] Скачать Release.zip и проверить содержимое

### 4. Проверка релиза
```powershell
# Скачать Release.zip из GitHub
# Проверить SHA256
$hash = Get-FileHash .\Release.zip -Algorithm SHA256
$expectedHash = Get-Content .\Release.zip.sha256

if ($hash.Hash -eq $expectedHash.Trim()) {
    Write-Host "✓ Checksum verified!" -ForegroundColor Green
} else {
    Write-Host "✗ Checksum mismatch!" -ForegroundColor Red
}

# Распаковать и проверить содержимое
Expand-Archive Release.zip -DestinationPath Test
Get-ChildItem Test
```

## После публикации

### 1. Создать первое объявление
- [ ] GitHub → Discussions → Announcements
- [ ] Создать пост о релизе v1.0.0
- [ ] Описать основные функции
- [ ] Дать ссылку на Release

### 2. Документация
- [ ] Проверить что README отображается корректно
- [ ] Проверить что все ссылки работают
- [ ] Убедиться что badges показывают статус

### 3. Мониторинг
- [ ] GitHub → Insights → Traffic
- [ ] GitHub → Security → Code scanning
- [ ] Проверить что CodeQL отработал
- [ ] Настроить уведомления (Settings → Notifications)

## Текущая разработка

### Процесс работы
```powershell
# 1. Создать ветку для фичи
git checkout -b feature/new-awesome-feature

# 2. Внести изменения
# ... coding ...

# 3. Коммит
git add .
git commit -m "Add: новая функция"

# 4. Push в GitHub
git push origin feature/new-awesome-feature

# 5. Создать Pull Request на GitHub
# 6. Дождаться прохождения CI
# 7. Merge в main
```

### Создание новых релизов
```powershell
# Патч (исправление бага): 1.0.0 → 1.0.1
git tag -a v1.0.1 -m "Fix: описание исправления"

# Minor (новая фича): 1.0.1 → 1.1.0
git tag -a v1.1.0 -m "Add: новая функция"

# Major (breaking changes): 1.1.0 → 2.0.0
git tag -a v2.0.0 -m "Breaking: значительные изменения"

# Push тега
git push origin v1.x.x
```

## Troubleshooting

### GitHub Actions не запускается
1. Settings → Actions → General
2. "Actions permissions": Allow all actions and reusable workflows
3. "Workflow permissions": Read and write permissions

### Release.yml падает с ошибкой
1. GitHub → Actions → Create Release → Logs
2. Найти ошибку
3. Проверить что в workflow правильный путь к .csproj
4. Проверить права: permissions: contents: write

### Badge не работает
- Проверить URL в README.md
- Формат: `https://github.com/USERNAME/REPO/actions/workflows/FILE.yml/badge.svg`
- Заменить USERNAME и REPO

### Dependabot алерты
- GitHub → Security → Dependabot alerts
- Review каждый алерт
- Обновить зависимости в .csproj
- Проверить что всё работает
- Коммит и push

## Финальная проверка

### Всё готово если:
- [x] Код на GitHub
- [ ] Build badge зелёный ✓
- [ ] Первый релиз создан
- [ ] Release.zip доступен для скачивания
- [ ] README выглядит профессионально
- [ ] Security настроена
- [ ] Topics добавлены
- [ ] Описание заполнено

## 🎉 Проект опубликован!

Поздравляю! Ваш проект теперь доступен сообществу.

### Следующие шаги:
1. Продвижение в соцсетях (опционально)
2. Мониторинг Issues и Pull Requests
3. Ответы на вопросы пользователей
4. Планирование следующих версий
5. Регулярные обновления безопасности

---

Дата создания чеклиста: 17 января 2026
Удачи с проектом! 🚀
