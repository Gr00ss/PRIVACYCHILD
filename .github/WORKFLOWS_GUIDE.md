# GitHub Actions - Руководство

В проекте настроены автоматические CI/CD процессы через GitHub Actions.

## Workflows

### 1. Build and Test (`build.yml`)

**Когда запускается:**
- При каждом push в `main` или `develop`
- При создании Pull Request

**Что делает:**
- Проверяет сборку проекта
- Создает self-contained exe
- Проверяет размер файла (должен быть ~50MB)
- Загружает артефакты для скачивания

**Просмотр:**
```
GitHub → Actions → Build and Test
```

### 2. Create Release (`release.yml`)

**Когда запускается:**
- При создании git тега формата `v*.*.*`

**Что делает:**
- Собирает проект в Release конфигурации
- Создает Release.zip со всеми файлами
- Генерирует SHA256 checksum
- Публикует GitHub Release
- Прикрепляет архив и checksum

**Как создать релиз:**

```powershell
# 1. Убедитесь что все изменения закоммичены
git status

# 2. Создайте и отправьте тег
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0

# 3. Дождитесь окончания GitHub Action (2-3 минуты)

# 4. Проверьте релиз
# GitHub → Releases → должен появиться новый релиз
```

**Что будет в релизе:**
- `Release.zip` - готовый к развертыванию пакет
- `Release.zip.sha256` - контрольная сумма
- Автоматически сгенерированные Release Notes

### 3. CodeQL Security Analysis (`codeql.yml`)

**Когда запускается:**
- При push в `main`
- При Pull Request
- Еженедельно (по понедельникам в 00:00)

**Что делает:**
- Анализирует код на уязвимости
- Проверяет качество кода
- Создает отчеты в Security → Code scanning

**Просмотр результатов:**
```
GitHub → Security → Code scanning alerts
```

## Badges в README

После первого push обновите badges в README.md:

Замените `YOURUSERNAME` на ваше имя пользователя GitHub:

```markdown
[![Build](https://github.com/YOURUSERNAME/PRIVACYCHILD/actions/workflows/build.yml/badge.svg)](https://github.com/YOURUSERNAME/PRIVACYCHILD/actions/workflows/build.yml)
[![Release](https://github.com/YOURUSERNAME/PRIVACYCHILD/actions/workflows/release.yml/badge.svg)](https://github.com/YOURUSERNAME/PRIVACYCHILD/actions/workflows/release.yml)
[![CodeQL](https://github.com/YOURUSERNAME/PRIVACYCHILD/actions/workflows/codeql.yml/badge.svg)](https://github.com/YOURUSERNAME/PRIVACYCHILD/actions/workflows/codeql.yml)
```

## Процесс релиза (пошагово)

### Подготовка

1. **Проверьте что всё работает локально**
   ```powershell
   dotnet build -c Release
   dotnet publish -c Release -r win-x64 --self-contained
   ```

2. **Обновите CHANGELOG** (если есть)
   - Перечислите изменения с последнего релиза
   - Укажите breaking changes
   - Упомяните новые фичи и исправления

3. **Проверьте версию в проекте**
   ```xml
   <!-- sample1.csproj -->
   <Version>1.0.0</Version>
   ```

### Создание релиза

4. **Закоммитьте все изменения**
   ```powershell
   git add .
   git commit -m "Prepare for release v1.0.0"
   git push origin main
   ```

5. **Создайте тег**
   ```powershell
   # Формат: v{MAJOR}.{MINOR}.{PATCH}
   git tag -a v1.0.0 -m "Release version 1.0.0"
   git push origin v1.0.0
   ```

6. **Следите за процессом**
   ```
   GitHub → Actions → Create Release
   ```
   
   Workflow займет примерно 2-3 минуты:
   - ✓ Checkout code
   - ✓ Setup .NET
   - ✓ Build and publish
   - ✓ Create Release package
   - ✓ Calculate SHA256
   - ✓ Create GitHub Release

7. **Проверьте релиз**
   ```
   GitHub → Releases → Latest
   ```
   
   Должно быть:
   - Release.zip (~51-52 MB)
   - Release.zip.sha256
   - Release Notes

### После релиза

8. **Объявите о релизе**
   - Создайте Discussion/Announcement
   - Обновите документацию
   - Сообщите пользователям

9. **Проверьте скачивание**
   ```powershell
   # Скачайте Release.zip из GitHub
   # Проверьте SHA256
   $hash = Get-FileHash Release.zip -Algorithm SHA256
   $expectedHash = Get-Content Release.zip.sha256
   if ($hash.Hash -eq $expectedHash) {
       Write-Host "Checksum OK" -ForegroundColor Green
   }
   ```

## Semantic Versioning

Используйте [SemVer](https://semver.org/):

- **MAJOR** (1.0.0 → 2.0.0) - несовместимые изменения API
- **MINOR** (1.0.0 → 1.1.0) - новые функции (обратно совместимо)
- **PATCH** (1.0.0 → 1.0.1) - исправления багов

Примеры:
```powershell
# Исправление бага
git tag -a v1.0.1 -m "Fix: Telegram bot reconnection issue"

# Новая фича
git tag -a v1.1.0 -m "Add: Network traffic statistics"

# Breaking change
git tag -a v2.0.0 -m "Breaking: New database schema"
```

## Troubleshooting

### Workflow не запустился

Проверьте:
```
GitHub → Actions → All workflows
```

Возможные причины:
- Тег не в формате `v*.*.*`
- Actions отключены в настройках репозитория
- Ошибка в YAML файле workflow

### Сборка упала

1. Откройте логи workflow
2. Найдите красный крестик ❌
3. Изучите ошибку
4. Исправьте и пуште снова

### Релиз создался но пустой

Проверьте что:
- `sample1.exe` собрался (логи Build step)
- `Release.zip` создался (логи Create Release package)
- У GitHub есть права на создание релизов (Settings → Actions → Permissions)

### SHA256 не совпадает

Файл мог быть поврежден при загрузке. Скачайте заново.

## Безопасность

### Secrets в GitHub

Если нужны приватные данные в workflows:

```
GitHub → Settings → Secrets and variables → Actions → New repository secret
```

Использование в workflow:
```yaml
- name: Use secret
  run: echo "${{ secrets.MY_SECRET }}"
```

### Permissions

Workflows имеют ограниченные права. Для релизов используется:

```yaml
permissions:
  contents: write  # Для создания releases
```

## Дополнительно

### Локальное тестирование workflows

Используйте [act](https://github.com/nektos/act):

```powershell
# Установка через chocolatey
choco install act-cli

# Запуск workflow локально
act push
```

### Кэширование зависимостей

Для ускорения можно добавить в workflow:

```yaml
- name: Cache NuGet packages
  uses: actions/cache@v3
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
```

---

Автоматизация готова к использованию! 🚀
