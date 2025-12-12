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

* принимает и сохраняет файл
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
2. Gateway → FileStoring
3. FileStoring сохраняет файл и метаданные
4. Gateway вызывает Analysis
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
curl -v -http://localhost:5000/works/submit \
  -F "file=@example.txt" \
  -F "studentName=Ноговицын" \
  -F "assignmentId=hw3"
```

---

# 2. Получить все работы по заданию

```
curl -v http://localhost:5000/works/hw3/reports
```

---

# 3. Получить отчет по работе

```
curl http://localhost:5002/analysis/trigger \
  -d "{
  "submissionId": "de91c221-a913-40fb-a3e5-0d80ea8113f7"
}"
```

---

# 4. Получить все отчеты по заданию

```
curl http://localhost:5002/analysis/reports/by-work/hw3
```

---


# Внутренние сервисы (если нужно вызвать напрямую)

### FileStoring - загрузить файл:

```
curl http://localhost:5001/files/upload \
  -F "File=@example.txt" \
  -F "StudentName=Ноговицын" \
  -F "AssignmentId=hw3"
```

### Analysis - запустить анализ:

```
curl http://localhost:5002/analysis/trigger \
  -d "{
  "submissionId": "de91c221-a913-40fb-a3e5-0d80ea8113f7"
}"
```

---
