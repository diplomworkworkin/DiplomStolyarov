import os
from datetime import date, timedelta
from typing import Generator

from sqlalchemy import create_engine, event
from sqlalchemy.engine import Engine
from sqlalchemy.orm import Session, sessionmaker

DEFAULT_SQLITE_URL = "sqlite:///./school_schedule.db"
SQLALCHEMY_DATABASE_URL = os.getenv("DATABASE_URL", DEFAULT_SQLITE_URL)

engine_kwargs = {"pool_pre_ping": True}
if SQLALCHEMY_DATABASE_URL.startswith("sqlite"):
    engine_kwargs["connect_args"] = {"check_same_thread": False}

engine = create_engine(SQLALCHEMY_DATABASE_URL, **engine_kwargs)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

if SQLALCHEMY_DATABASE_URL.startswith("sqlite"):
    @event.listens_for(Engine, "connect")
    def _set_sqlite_pragma(dbapi_connection, _connection_record) -> None:
        # SQLite disables FK checks by default, this keeps relation checks consistent.
        cursor = dbapi_connection.cursor()
        cursor.execute("PRAGMA foreign_keys=ON")
        cursor.close()


def _current_week_start_iso() -> str:
    today = date.today()
    monday = today - timedelta(days=today.weekday())
    return monday.isoformat()


def _has_lesson_week_column(cursor) -> bool:
    cursor.execute("PRAGMA table_info('Lessons')")
    columns = [row[1] for row in cursor.fetchall()]
    return "WeekStartDate" in columns


def _has_unique_index(cursor, columns: list[str]) -> bool:
    cursor.execute("PRAGMA index_list('Lessons')")
    indexes = cursor.fetchall()

    for index in indexes:
        # index format: (seq, name, unique, origin, partial) for modern sqlite
        unique = index[2] if len(index) > 2 else 0
        if unique != 1:
            continue

        index_name = index[1]
        cursor.execute(f"PRAGMA index_info('{index_name}')")
        index_columns = [row[2] for row in cursor.fetchall()]
        if index_columns == columns:
            return True

    return False


def _has_week_aware_uniques(cursor) -> bool:
    return (
        _has_unique_index(cursor, ["AcademicClassId", "WeekStartDate", "DayOfWeek", "LessonIndex"])
        and _has_unique_index(cursor, ["TeacherId", "WeekStartDate", "DayOfWeek", "LessonIndex"])
        and _has_unique_index(cursor, ["ClassroomId", "WeekStartDate", "DayOfWeek", "LessonIndex"])
    )


def _rebuild_lessons_table_for_weeks() -> None:
    raw = engine.raw_connection()
    cursor = raw.cursor()
    week_start = _current_week_start_iso()

    try:
        cursor.execute("PRAGMA foreign_keys=OFF")
        cursor.execute("BEGIN")

        cursor.execute(
            """
            CREATE TABLE Lessons_new (
                Id INTEGER NOT NULL PRIMARY KEY,
                WeekStartDate VARCHAR(10) NOT NULL,
                DayOfWeek INTEGER NOT NULL,
                LessonIndex INTEGER NOT NULL,
                TeacherId INTEGER NOT NULL REFERENCES Teachers (Id),
                SubjectId INTEGER NOT NULL REFERENCES Subjects (Id),
                AcademicClassId INTEGER NOT NULL REFERENCES AcademicClasses (Id),
                ClassroomId INTEGER REFERENCES Classrooms (Id),
                UNIQUE (AcademicClassId, WeekStartDate, DayOfWeek, LessonIndex),
                UNIQUE (TeacherId, WeekStartDate, DayOfWeek, LessonIndex),
                UNIQUE (ClassroomId, WeekStartDate, DayOfWeek, LessonIndex)
            )
            """
        )

        has_week_column = _has_lesson_week_column(cursor)
        if has_week_column:
            cursor.execute(
                """
                INSERT INTO Lessons_new (
                    Id, WeekStartDate, DayOfWeek, LessonIndex, TeacherId, SubjectId, AcademicClassId, ClassroomId
                )
                SELECT
                    Id,
                    COALESCE(NULLIF(WeekStartDate, ''), ?),
                    DayOfWeek,
                    LessonIndex,
                    TeacherId,
                    SubjectId,
                    AcademicClassId,
                    ClassroomId
                FROM Lessons
                """,
                (week_start,),
            )
        else:
            cursor.execute(
                """
                INSERT INTO Lessons_new (
                    Id, WeekStartDate, DayOfWeek, LessonIndex, TeacherId, SubjectId, AcademicClassId, ClassroomId
                )
                SELECT
                    Id,
                    ?,
                    DayOfWeek,
                    LessonIndex,
                    TeacherId,
                    SubjectId,
                    AcademicClassId,
                    ClassroomId
                FROM Lessons
                """,
                (week_start,),
            )

        cursor.execute("DROP TABLE Lessons")
        cursor.execute("ALTER TABLE Lessons_new RENAME TO Lessons")
        cursor.execute("CREATE INDEX IF NOT EXISTS ix_Lessons_Id ON Lessons (Id)")

        cursor.execute("COMMIT")
    except Exception:
        cursor.execute("ROLLBACK")
        raise
    finally:
        cursor.execute("PRAGMA foreign_keys=ON")
        cursor.close()
        raw.close()


def _ensure_lessons_week_schema() -> None:
    if not SQLALCHEMY_DATABASE_URL.startswith("sqlite"):
        return

    raw = engine.raw_connection()
    cursor = raw.cursor()
    try:
        cursor.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='Lessons'")
        exists = cursor.fetchone() is not None
        if not exists:
            return

        has_week = _has_lesson_week_column(cursor)
        if has_week and _has_week_aware_uniques(cursor):
            return
    finally:
        cursor.close()
        raw.close()

    _rebuild_lessons_table_for_weeks()


def _has_workload_year_hours_column(cursor) -> bool:
    cursor.execute("PRAGMA table_info('Workloads')")
    columns = [row[1] for row in cursor.fetchall()]
    return "YearHours" in columns


def _ensure_workloads_year_hours_schema() -> None:
    if not SQLALCHEMY_DATABASE_URL.startswith("sqlite"):
        return

    raw = engine.raw_connection()
    cursor = raw.cursor()
    try:
        cursor.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='Workloads'")
        exists = cursor.fetchone() is not None
        if not exists:
            return

        if not _has_workload_year_hours_column(cursor):
            cursor.execute(
                "ALTER TABLE Workloads ADD COLUMN YearHours INTEGER NOT NULL DEFAULT 0"
            )

        # Backfill existing rows with a predictable value.
        cursor.execute(
            """
            UPDATE Workloads
            SET YearHours = CASE
                WHEN HoursPerWeek > 0 THEN HoursPerWeek * 34
                ELSE 34
            END
            WHERE YearHours IS NULL OR YearHours <= 0
            """
        )
        raw.commit()
    finally:
        cursor.close()
        raw.close()


def init_db() -> None:
    from app.core.seed import seed_database
    from app.models.database import Base

    Base.metadata.create_all(bind=engine)
    _ensure_lessons_week_schema()
    _ensure_workloads_year_hours_schema()
    with SessionLocal() as db:
        seed_database(db)


def get_db() -> Generator[Session, None, None]:
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
