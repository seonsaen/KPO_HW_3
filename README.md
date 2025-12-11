# КПО-ДЗ-3 — Микросервисная система проверки студенческих работ



---

## Описание проекта

Система реализует проверку студенческих работ на плагиат с использованием распределенной микросервисной архитектуры.

Состоит из трёх независимых сервисов:

1. **Gateway** - API, единственная публичная точка входа.
2. **FileStoring** - сервис хранения файлов и метаданных.
3. **Analysis** - сервис анализа файлов и генерации отчетов.

Все сервисы работают в контейнерах и общаются через HTTP.

---

## Архитектура системы


### Gateway

* принимает файлы от студентов
* пересылает их в FileStoring
* запускает анализ на Analysis
* отдает клиенту результаты

### FileStoring

* принимает и сохраняет файл (`multipart/form-data`)
* сохраняет метаданные в SQLite
* отдает файлы и список работ

### Analysis

* получает файл по `workId` из FileStoring
* получает список всех работ по заданию
* рассчитывает схожесть
* формирует отчет
* сохраняет JSON отчеты + метаданные в SQLite

---

## Пользовательские сценарии

### Загрузка работы

1. Клиент отправляет файл → Gateway
2. Gateway → FileStoring (`files/upload`)
3. FileStoring сохраняет файл и метаданные
4. Gateway вызывает Analysis (`analysis/trigger`)
5. Analysis получает файл и список работ из FileStoring
6. Анализ выполняется, генерируется отчёт
7. Gateway возвращает результат клиенту

### Получение отчета

1. Клиент → Gateway → Analysis
2. Analysis читает JSON отчета
3. Возвращает результат

### Получение списка всех работ

1. Клиент → Gateway → FileStoring
2. FileStoring отдаёт список

---

## Как запустить проект


### 1. Собрать и запустить все сервисы:

```
docker compose up --build
```

Сервисы поднимутся на:

* **Gateway:** [http://localhost:5000/swagger](http://localhost:5000/swagger)
* **FileStoring:** [http://localhost:5001/swagger](http://localhost:5001/swagger)
* **Analysis:** [http://localhost:5002/swagger](http://localhost:5002/swagger)

---

## Примеры запросов

---

# 1. Загрузка работы студента

```
curl -v -X POST http://localhost:5000/submit \
  -F "file=@test1.txt" \
  -F "studentName=Иванов" \
  -F "assignmentId=hw1"
```

---

# 2. Получить все работы по заданию

```
curl -v http://localhost:5000/files/assignment/hw1
```

---

# 3. Получить отчет по работе

```
curl -v http://localhost:5000/reports/work/w_168234502
```

---

# 4. Получить все отчеты по заданию

```
curl -v http://localhost:5000/reports/assignment/hw1
```

---

# 5. Проверить список отчетов напрямую из Analysis

```
curl -v http://localhost:5002/analysis/reports
```

---

# Внутренние сервисы (если нужно вызвать напрямую)

### FileStoring - загрузить файл:

```
curl -v -X POST http://localhost:5001/files/upload \
  -F "file=@test1.txt" \
  -F "studentName=Иванов" \
  -F "assignmentId=hw1"
```

### Analysis - запустить анализ:

```
curl -v -X POST http://localhost:5002/analysis/trigger \
  -H "Content-Type: application/json" \
  -d '{"workId":"w_168234502","studentName":"Иванов","assignmentId":"hw1"}'
```

---
