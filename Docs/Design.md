# Stepik Analytics Desktop — Design Notes

## NuGet пакеты
- Avalonia 11 (UI + Desktop + Fluent + DataGrid)
- LiveChartsCore.SkiaSharpView.Avalonia (графики)
- Microsoft.EntityFrameworkCore + Sqlite + Design (SQLite + миграции)
- CsvHelper (экспорт CSV)

## Структура папок и ключевые классы
- **/App**
  - `Program.cs` — bootstrap Avalonia.
  - `App.axaml` — темы и DataTemplates.
  - `App.axaml.cs` — wiring сервисов/VM, auto migrations.
- **/Views** — XAML экраны.
  - `MainWindow.axaml` — TabControl навигация.
  - `CoursesView.axaml` — добавление курсов и таблица.
  - `DashboardView.axaml` — KPI + графики.
  - `SyncView.axaml` — запуск/остановка синка + лог.
  - `SettingsView.axaml` — токен, backfill, авто-синк, экспорт.
- **/ViewModels**
  - `MainWindowViewModel` — набор секций.
  - `CoursesViewModel` — CRUD курсов + запуск синка.
  - `DashboardViewModel` — фильтры, KPI, серии графиков.
  - `SyncViewModel` — прогресс синка, логи, последние SyncRuns.
  - `SettingsViewModel` — настройки и экспорт.
- **/Domain**
  - `TimeRange` — вычисление периодов (день/неделя/месяц/год + delta).
  - `Enums` — SyncStatus/PeriodKind.
  - `MetricModels` — KPI карточки.
- **/Infrastructure**
  - `StepikApiClient` — HTTP + pagination + retry, возвращает `ApiResult`.
  - `Auth/TokenAuthProvider` — заголовок Bearer.
- **/Data**
  - `AppDbContext` — EF Core, Fluent mappings.
  - `Entities` — CourseEntity/SyncRunEntity/DailyMetricEntity/AttemptRawEntity.
  - `SqliteDbContextFactory` — путь БД + auto-migrate.
- **/Services**
  - `SyncService` — backfill + incremental sync, raw + daily.
  - `AggregationService` — агрегаты по периодам.
  - `ExportService` — CSV экспорт.
  - `SchedulerService` — авто-синк 1 раз/сутки.
  - `SettingsService` — JSON конфиг.
- **/Utils**
  - `RetryPolicy` — exponential backoff + jitter.
  - `UiLogger` — структурированный лог + вывод в UI.
  - `TimeZoneProvider` — конвертации Europe/Riga и др.

## Схема БД (Entities)
- **Courses**
  - `Id` (PK)
  - `CourseId` (unique)
  - `Title`, `Url`
  - `AddedAt`, `LastSyncAt`, `LastSyncedEventAt`
  - `SyncStatus`, `LastError`
- **SyncRuns**
  - `Id` (PK)
  - `CourseId` (FK -> Courses)
  - `StartedAt`, `FinishedAt`, `Status`, `ErrorText`
- **DailyMetrics** (предагрегация)
  - `Id` (PK)
  - `CourseId` (FK -> Courses)
  - `Date` (yyyy-MM-dd)
  - `TotalAttempts`, `CorrectAttempts`, `WrongAttempts`
  - `NewStudents`, `CertificatesIssued`, `ReviewsCount`, `ActiveUsers`
  - `RatingValue`
  - `ReviewsAvg`, `ReviewsMedian`, `ReviewsStar1..5`
  - Unique: `(CourseId, Date)`
- **AttemptsRaw** (обоснование ниже)
  - `AttemptId` (unique)
  - `CourseId`, `UserId`, `CreatedAt`, `IsCorrect`
  - Индексы: `(CourseId, CreatedAt)`, `(CourseId, UserId, CreatedAt)`

**Почему нужны raw attempts:** без `AttemptId/UserId` невозможно корректно посчитать `ActiveUsers` (distinct users/day) и корректные/некорректные решения. Поэтому храним `AttemptsRaw` и строим daily агрегации из них.

## Таблица соответствий метрик
| Метрика | Stepik ресурс/endpoint | Поля | Расчет |
|---|---|---|---|
| Всего/правильные/неправильные решения | **уточнить в Stepik API** (attempts/solutions) | attempt_id, user_id, created_at, is_correct | count / count where is_correct |
| Активные пользователи | **уточнить** (attempts) | user_id, created_at | count distinct user_id per day |
| Новые ученики (enrollments) | **уточнить** | user_id, created_at | count per day |
| Сертификаты | **уточнить** | user_id, issued_at | count per day |
| Отзывы (count + распределение 1-5) | **уточнить** | review_id, stars, created_at | count per day + distribution |
| Rating во времени | **уточнить** | rating, recorded_at | latest per day |
| Репутация/знания | **уточнить** | TBD | если недоступно — "недоступно" |

## Псевдокод SyncService (инкрементальный)
```
SyncCourse(courseId, ct):
  course = db.Courses by CourseId
  from = course.LastSyncedEventAt ?? now - BackfillDays
  to = now
  for each metric endpoint:
    if endpoint not available -> log "недоступно"
    else fetch paged data with retry/rate-limit handling
  upsert AttemptsRaw (dedupe by AttemptId)
  update DailyMetrics for attempts + active users
  upsert daily counts for enrollments/certificates/reviews/ratings
  course.LastSyncedEventAt = to
  course.LastSyncAt = now
  write SyncRun status
```

## ViewModel и Bindings (ключевые)
- `CoursesViewModel`
  - `CourseInput`, `AddCourseCommand`, `Courses` (DataGrid)
  - `SyncCourseCommand`, `DeleteCourseCommand`
- `DashboardViewModel`
  - `SelectedCourses`, `SelectedPeriod`, `SelectedDate`, `RefreshCommand`
  - `MetricCards` + `AttemptsSeries`/`NewStudentsSeries`/`RatingSeries`
- `SyncViewModel`
  - `SyncAllCommand`, `StopCommand`
  - `LogLines`, `RecentRuns`, `Progress`
- `SettingsViewModel`
  - `ApiToken`, `BackfillDays`, `AutoSyncEnabled`, `SelectedTimeZone`
  - `ExportCsvCommand`, `ClearCourseCommand`

## Checklist приемки
- [ ] Один `.sln` и один `.csproj`.
- [ ] .NET 10 target.
- [ ] EF Core + SQLite + auto migrations.
- [ ] Async sync + cancellation, UI responsive.
- [ ] Offline режим работает на локальных данных.
- [ ] KPI и графики по периодам (день/неделя/месяц/год).
- [ ] Delta к предыдущему периоду.
- [ ] Сравнение нескольких курсов.
- [ ] CSV экспорт.
- [ ] Логи отображаются в UI.
- [ ] Не доступные метрики помечены как "недоступно".
