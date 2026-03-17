from contextlib import asynccontextmanager
from datetime import date, datetime, timedelta
from typing import List, Optional, Type

from fastapi import Depends, FastAPI, HTTPException, Response, status
from pydantic import AliasChoices, BaseModel, Field
from sqlalchemy.exc import IntegrityError
from sqlalchemy import func, tuple_
from sqlalchemy.orm import Session

from app.core.database import get_db, init_db
from app.models.database import (
    AcademicClass,
    Classroom,
    Lesson,
    Subject,
    Teacher,
    User,
    UserRole,
    Workload,
)
from app.schemas import schemas


@asynccontextmanager
async def lifespan(_app: FastAPI):
    init_db()
    yield


app = FastAPI(
    title="School Schedule API",
    description="API for managing school schedules and resources",
    lifespan=lifespan,
)

SUPPORTED_USER_ROLES = {int(UserRole.Admin), int(UserRole.Teacher)}


class LoginRequest(BaseModel):
    Username: str = Field(validation_alias=AliasChoices("Username", "username"))
    Password: str = Field(validation_alias=AliasChoices("Password", "password"))


def get_current_week_start() -> str:
    today = date.today()
    monday = today - timedelta(days=today.weekday())
    return monday.isoformat()


def normalize_week_start(week_start: Optional[str]) -> str:
    if week_start is None or not week_start.strip():
        return get_current_week_start()

    try:
        parsed = datetime.strptime(week_start.strip(), "%Y-%m-%d").date()
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="week_start must be in YYYY-MM-DD format",
        ) from exc

    monday = parsed - timedelta(days=parsed.weekday())
    return monday.isoformat()


def ensure_supported_user_role(role: Optional[int]) -> None:
    if role is None or role not in SUPPORTED_USER_ROLES:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Only Admin (0) and Teacher (1) roles are supported.",
        )


def get_entity_or_404(
    db: Session, model: Type, entity_id: int, entity_name: str
):
    entity = db.get(model, entity_id)
    if entity is None:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"{entity_name} with id={entity_id} not found",
        )
    return entity


def commit_session(db: Session, conflict_message: str) -> None:
    try:
        db.commit()
    except IntegrityError as exc:
        db.rollback()
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT, detail=conflict_message
        ) from exc


def ensure_foreign_keys_exist(
    db: Session,
    *,
    subject_id: Optional[int] = None,
    teacher_id: Optional[int] = None,
    class_id: Optional[int] = None,
    classroom_id: Optional[int] = None,
) -> None:
    if subject_id is not None:
        get_entity_or_404(db, Subject, subject_id, "Subject")
    if teacher_id is not None:
        get_entity_or_404(db, Teacher, teacher_id, "Teacher")
    if class_id is not None:
        get_entity_or_404(db, AcademicClass, class_id, "AcademicClass")
    if classroom_id is not None:
        get_entity_or_404(db, Classroom, classroom_id, "Classroom")


def normalize_workload_hours(
    *, hours_per_week: int, year_hours: Optional[int]
) -> tuple[int, int]:
    if hours_per_week < 1 or hours_per_week > 10:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="HoursPerWeek must be in range 1..10",
        )

    resolved_year_hours = year_hours if year_hours is not None else hours_per_week * 34
    if resolved_year_hours < hours_per_week:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="YearHours must be greater than or equal to HoursPerWeek",
        )

    return hours_per_week, resolved_year_hours


def attach_workload_remaining_hours(db: Session, workloads: List[Workload]) -> None:
    if not workloads:
        return

    keys = {
        (item.TeacherId, item.SubjectId, item.AcademicClassId)
        for item in workloads
    }
    key_list = list(keys)

    count_rows = (
        db.query(
            Lesson.TeacherId,
            Lesson.SubjectId,
            Lesson.AcademicClassId,
            func.count(Lesson.Id),
        )
        .filter(
            tuple_(Lesson.TeacherId, Lesson.SubjectId, Lesson.AcademicClassId).in_(key_list)
        )
        .group_by(Lesson.TeacherId, Lesson.SubjectId, Lesson.AcademicClassId)
        .all()
    )

    used_hours_by_key = {
        (teacher_id, subject_id, class_id): count
        for teacher_id, subject_id, class_id, count in count_rows
    }

    for workload in workloads:
        key = (workload.TeacherId, workload.SubjectId, workload.AcademicClassId)
        used_hours = int(used_hours_by_key.get(key, 0))
        year_hours = int(getattr(workload, "YearHours", 0) or 0)
        workload.RemainingHours = max(0, year_hours - used_hours)


def validate_curator_uniqueness(
    db: Session, curator_teacher_id: Optional[int], current_class_id: Optional[int] = None
) -> None:
    if curator_teacher_id is None:
        return

    ensure_foreign_keys_exist(db, teacher_id=curator_teacher_id)

    query = db.query(AcademicClass).filter(
        AcademicClass.CuratorTeacherId == curator_teacher_id
    )
    if current_class_id is not None:
        query = query.filter(AcademicClass.Id != current_class_id)

    existing = query.first()
    if existing is not None:
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="This teacher is already a curator of another class",
        )


@app.get("/")
def read_root():
    return {"message": "Welcome to School Schedule API"}


@app.get("/health")
def read_health():
    return {"status": "ok"}


@app.post("/auth/login", response_model=schemas.User)
def login(payload: LoginRequest, db: Session = Depends(get_db)):
    user = (
        db.query(User)
        .filter(User.Username == payload.Username, User.Password == payload.Password)
        .first()
    )
    if user is None:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid username or password",
        )
    return user


@app.get("/subjects", response_model=List[schemas.Subject])
def read_subjects(skip: int = 0, limit: int = 100, db: Session = Depends(get_db)):
    return db.query(Subject).offset(skip).limit(limit).all()


@app.get("/subjects/{subject_id}", response_model=schemas.Subject)
def read_subject(subject_id: int, db: Session = Depends(get_db)):
    return get_entity_or_404(db, Subject, subject_id, "Subject")


@app.post("/subjects", response_model=schemas.Subject, status_code=status.HTTP_201_CREATED)
def create_subject(subject: schemas.SubjectCreate, db: Session = Depends(get_db)):
    db_subject = Subject(**subject.model_dump())
    db.add(db_subject)
    commit_session(db, "Failed to create subject due to a data conflict")
    db.refresh(db_subject)
    return db_subject


@app.patch("/subjects/{subject_id}", response_model=schemas.Subject)
def update_subject(
    subject_id: int, subject: schemas.SubjectUpdate, db: Session = Depends(get_db)
):
    db_subject = get_entity_or_404(db, Subject, subject_id, "Subject")
    payload = subject.model_dump(exclude_unset=True)

    for field, value in payload.items():
        setattr(db_subject, field, value)

    commit_session(db, "Failed to update subject due to a data conflict")
    db.refresh(db_subject)
    return db_subject


@app.delete("/subjects/{subject_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_subject(subject_id: int, db: Session = Depends(get_db)):
    db_subject = get_entity_or_404(db, Subject, subject_id, "Subject")

    db.query(Lesson).filter(Lesson.SubjectId == subject_id).delete(synchronize_session=False)
    db.query(Workload).filter(Workload.SubjectId == subject_id).delete(
        synchronize_session=False
    )
    db.query(Teacher).filter(Teacher.SubjectId == subject_id).update(
        {Teacher.SubjectId: None}, synchronize_session=False
    )
    db.delete(db_subject)
    commit_session(db, "Failed to delete subject due to a data conflict")
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@app.get("/classrooms", response_model=List[schemas.Classroom])
def read_classrooms(skip: int = 0, limit: int = 100, db: Session = Depends(get_db)):
    return db.query(Classroom).offset(skip).limit(limit).all()


@app.get("/classrooms/{classroom_id}", response_model=schemas.Classroom)
def read_classroom(classroom_id: int, db: Session = Depends(get_db)):
    return get_entity_or_404(db, Classroom, classroom_id, "Classroom")


@app.post(
    "/classrooms", response_model=schemas.Classroom, status_code=status.HTTP_201_CREATED
)
def create_classroom(classroom: schemas.ClassroomCreate, db: Session = Depends(get_db)):
    db_classroom = Classroom(**classroom.model_dump())
    db.add(db_classroom)
    commit_session(db, "Failed to create classroom due to a data conflict")
    db.refresh(db_classroom)
    return db_classroom


@app.patch("/classrooms/{classroom_id}", response_model=schemas.Classroom)
def update_classroom(
    classroom_id: int,
    classroom: schemas.ClassroomUpdate,
    db: Session = Depends(get_db),
):
    db_classroom = get_entity_or_404(db, Classroom, classroom_id, "Classroom")
    payload = classroom.model_dump(exclude_unset=True)

    for field, value in payload.items():
        setattr(db_classroom, field, value)

    commit_session(db, "Failed to update classroom due to a data conflict")
    db.refresh(db_classroom)
    return db_classroom


@app.delete("/classrooms/{classroom_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_classroom(classroom_id: int, db: Session = Depends(get_db)):
    db_classroom = get_entity_or_404(db, Classroom, classroom_id, "Classroom")

    db.query(Lesson).filter(Lesson.ClassroomId == classroom_id).delete(
        synchronize_session=False
    )
    db.query(Teacher).filter(Teacher.ClassroomId == classroom_id).update(
        {Teacher.ClassroomId: None}, synchronize_session=False
    )
    db.delete(db_classroom)
    commit_session(db, "Failed to delete classroom due to a data conflict")
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@app.get("/teachers", response_model=List[schemas.Teacher])
def read_teachers(skip: int = 0, limit: int = 100, db: Session = Depends(get_db)):
    return db.query(Teacher).offset(skip).limit(limit).all()


@app.get("/teachers/{teacher_id}", response_model=schemas.Teacher)
def read_teacher(teacher_id: int, db: Session = Depends(get_db)):
    return get_entity_or_404(db, Teacher, teacher_id, "Teacher")


@app.post("/teachers", response_model=schemas.Teacher, status_code=status.HTTP_201_CREATED)
def create_teacher(teacher: schemas.TeacherCreate, db: Session = Depends(get_db)):
    ensure_foreign_keys_exist(
        db, subject_id=teacher.SubjectId, classroom_id=teacher.ClassroomId
    )

    db_teacher = Teacher(**teacher.model_dump())
    db.add(db_teacher)
    commit_session(db, "Failed to create teacher due to a data conflict")
    db.refresh(db_teacher)
    return db_teacher


@app.patch("/teachers/{teacher_id}", response_model=schemas.Teacher)
def update_teacher(
    teacher_id: int, teacher: schemas.TeacherUpdate, db: Session = Depends(get_db)
):
    db_teacher = get_entity_or_404(db, Teacher, teacher_id, "Teacher")
    payload = teacher.model_dump(exclude_unset=True)

    if "SubjectId" in payload:
        ensure_foreign_keys_exist(db, subject_id=payload["SubjectId"])
    if "ClassroomId" in payload:
        ensure_foreign_keys_exist(db, classroom_id=payload["ClassroomId"])

    for field, value in payload.items():
        setattr(db_teacher, field, value)

    commit_session(db, "Failed to update teacher due to a data conflict")
    db.refresh(db_teacher)
    return db_teacher


@app.delete("/teachers/{teacher_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_teacher(teacher_id: int, db: Session = Depends(get_db)):
    db_teacher = get_entity_or_404(db, Teacher, teacher_id, "Teacher")

    db.query(Lesson).filter(Lesson.TeacherId == teacher_id).delete(synchronize_session=False)
    db.query(Workload).filter(Workload.TeacherId == teacher_id).delete(
        synchronize_session=False
    )
    db.query(AcademicClass).filter(AcademicClass.CuratorTeacherId == teacher_id).update(
        {AcademicClass.CuratorTeacherId: None}, synchronize_session=False
    )
    db.query(User).filter(User.TeacherId == teacher_id).update(
        {User.TeacherId: None}, synchronize_session=False
    )
    db.delete(db_teacher)
    commit_session(db, "Failed to delete teacher due to a data conflict")
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@app.get("/classes", response_model=List[schemas.AcademicClass])
def read_classes(skip: int = 0, limit: int = 100, db: Session = Depends(get_db)):
    return db.query(AcademicClass).offset(skip).limit(limit).all()


@app.get("/classes/{class_id}", response_model=schemas.AcademicClass)
def read_class(class_id: int, db: Session = Depends(get_db)):
    return get_entity_or_404(db, AcademicClass, class_id, "AcademicClass")


@app.post(
    "/classes", response_model=schemas.AcademicClass, status_code=status.HTTP_201_CREATED
)
def create_class(academic_class: schemas.AcademicClassCreate, db: Session = Depends(get_db)):
    validate_curator_uniqueness(db, academic_class.CuratorTeacherId)

    db_class = AcademicClass(**academic_class.model_dump())
    db.add(db_class)
    commit_session(db, "Failed to create class due to a data conflict")
    db.refresh(db_class)
    return db_class


@app.patch("/classes/{class_id}", response_model=schemas.AcademicClass)
def update_class(
    class_id: int,
    academic_class: schemas.AcademicClassUpdate,
    db: Session = Depends(get_db),
):
    db_class = get_entity_or_404(db, AcademicClass, class_id, "AcademicClass")
    payload = academic_class.model_dump(exclude_unset=True)

    if "CuratorTeacherId" in payload:
        validate_curator_uniqueness(db, payload["CuratorTeacherId"], class_id)

    for field, value in payload.items():
        setattr(db_class, field, value)

    commit_session(db, "Failed to update class due to a data conflict")
    db.refresh(db_class)
    return db_class


@app.delete("/classes/{class_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_class(class_id: int, db: Session = Depends(get_db)):
    db_class = get_entity_or_404(db, AcademicClass, class_id, "AcademicClass")

    db.query(Lesson).filter(Lesson.AcademicClassId == class_id).delete(
        synchronize_session=False
    )
    db.query(Workload).filter(Workload.AcademicClassId == class_id).delete(
        synchronize_session=False
    )
    db.query(User).filter(User.AcademicClassId == class_id).update(
        {User.AcademicClassId: None}, synchronize_session=False
    )
    db.delete(db_class)
    commit_session(db, "Failed to delete class due to a data conflict")
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@app.get("/workloads", response_model=List[schemas.Workload])
def read_workloads(
    skip: int = 0,
    limit: int = 100,
    teacher_id: Optional[int] = None,
    class_id: Optional[int] = None,
    subject_id: Optional[int] = None,
    db: Session = Depends(get_db),
):
    query = db.query(Workload)
    if teacher_id is not None:
        query = query.filter(Workload.TeacherId == teacher_id)
    if class_id is not None:
        query = query.filter(Workload.AcademicClassId == class_id)
    if subject_id is not None:
        query = query.filter(Workload.SubjectId == subject_id)

    items = query.offset(skip).limit(limit).all()
    attach_workload_remaining_hours(db, items)
    return items


@app.get("/workloads/{workload_id}", response_model=schemas.Workload)
def read_workload(workload_id: int, db: Session = Depends(get_db)):
    workload = get_entity_or_404(db, Workload, workload_id, "Workload")
    attach_workload_remaining_hours(db, [workload])
    return workload


@app.post(
    "/workloads", response_model=schemas.Workload, status_code=status.HTTP_201_CREATED
)
def create_workload(workload: schemas.WorkloadCreate, db: Session = Depends(get_db)):
    ensure_foreign_keys_exist(
        db,
        teacher_id=workload.TeacherId,
        subject_id=workload.SubjectId,
        class_id=workload.AcademicClassId,
    )

    hours_per_week, year_hours = normalize_workload_hours(
        hours_per_week=workload.HoursPerWeek,
        year_hours=workload.YearHours,
    )

    payload = workload.model_dump()
    payload["HoursPerWeek"] = hours_per_week
    payload["YearHours"] = year_hours

    db_workload = Workload(**payload)
    db.add(db_workload)
    commit_session(db, "Failed to create workload due to a data conflict")
    db.refresh(db_workload)
    attach_workload_remaining_hours(db, [db_workload])
    return db_workload


@app.patch("/workloads/{workload_id}", response_model=schemas.Workload)
def update_workload(
    workload_id: int, workload: schemas.WorkloadUpdate, db: Session = Depends(get_db)
):
    db_workload = get_entity_or_404(db, Workload, workload_id, "Workload")
    payload = workload.model_dump(exclude_unset=True)

    ensure_foreign_keys_exist(
        db,
        teacher_id=payload.get("TeacherId"),
        subject_id=payload.get("SubjectId"),
        class_id=payload.get("AcademicClassId"),
    )

    resolved_hours_per_week = payload.get("HoursPerWeek", db_workload.HoursPerWeek)
    resolved_year_hours = payload.get("YearHours", db_workload.YearHours)
    resolved_hours_per_week, resolved_year_hours = normalize_workload_hours(
        hours_per_week=resolved_hours_per_week,
        year_hours=resolved_year_hours,
    )
    payload["HoursPerWeek"] = resolved_hours_per_week
    payload["YearHours"] = resolved_year_hours

    for field, value in payload.items():
        setattr(db_workload, field, value)

    commit_session(db, "Failed to update workload due to a data conflict")
    db.refresh(db_workload)
    attach_workload_remaining_hours(db, [db_workload])
    return db_workload


@app.delete("/workloads/{workload_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_workload(workload_id: int, db: Session = Depends(get_db)):
    db_workload = get_entity_or_404(db, Workload, workload_id, "Workload")
    db.delete(db_workload)
    commit_session(db, "Failed to delete workload due to a data conflict")
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@app.get("/lessons", response_model=List[schemas.Lesson])
def read_lessons(
    skip: int = 0,
    limit: int = 100,
    class_id: Optional[int] = None,
    teacher_id: Optional[int] = None,
    day_of_week: Optional[int] = None,
    week_start: Optional[str] = None,
    db: Session = Depends(get_db),
):
    query = db.query(Lesson)
    normalized_week_start = normalize_week_start(week_start)
    query = query.filter(Lesson.WeekStartDate == normalized_week_start)

    if class_id is not None:
        query = query.filter(Lesson.AcademicClassId == class_id)
    if teacher_id is not None:
        query = query.filter(Lesson.TeacherId == teacher_id)
    if day_of_week is not None:
        query = query.filter(Lesson.DayOfWeek == day_of_week)

    return query.offset(skip).limit(limit).all()


@app.get("/lessons/{lesson_id}", response_model=schemas.Lesson)
def read_lesson(lesson_id: int, db: Session = Depends(get_db)):
    return get_entity_or_404(db, Lesson, lesson_id, "Lesson")


@app.post("/lessons", response_model=schemas.Lesson, status_code=status.HTTP_201_CREATED)
def create_lesson(lesson: schemas.LessonCreate, db: Session = Depends(get_db)):
    week_start = normalize_week_start(lesson.WeekStartDate)

    ensure_foreign_keys_exist(
        db,
        teacher_id=lesson.TeacherId,
        subject_id=lesson.SubjectId,
        class_id=lesson.AcademicClassId,
        classroom_id=lesson.ClassroomId,
    )

    payload = lesson.model_dump()
    payload["WeekStartDate"] = week_start

    db_lesson = Lesson(**payload)
    db.add(db_lesson)
    commit_session(
        db,
        "Failed to create lesson. Check class/teacher/classroom time conflicts and relations.",
    )
    db.refresh(db_lesson)
    return db_lesson


@app.patch("/lessons/{lesson_id}", response_model=schemas.Lesson)
def update_lesson(
    lesson_id: int, lesson: schemas.LessonUpdate, db: Session = Depends(get_db)
):
    db_lesson = get_entity_or_404(db, Lesson, lesson_id, "Lesson")
    payload = lesson.model_dump(exclude_unset=True)
    if "WeekStartDate" in payload:
        payload["WeekStartDate"] = normalize_week_start(payload["WeekStartDate"])

    ensure_foreign_keys_exist(
        db,
        teacher_id=payload.get("TeacherId"),
        subject_id=payload.get("SubjectId"),
        class_id=payload.get("AcademicClassId"),
        classroom_id=payload.get("ClassroomId"),
    )

    for field, value in payload.items():
        setattr(db_lesson, field, value)

    commit_session(
        db,
        "Failed to update lesson. Check class/teacher/classroom time conflicts and relations.",
    )
    db.refresh(db_lesson)
    return db_lesson


@app.delete("/lessons/{lesson_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_lesson(lesson_id: int, db: Session = Depends(get_db)):
    db_lesson = get_entity_or_404(db, Lesson, lesson_id, "Lesson")
    db.delete(db_lesson)
    commit_session(db, "Failed to delete lesson due to a data conflict")
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@app.get("/users", response_model=List[schemas.User])
def read_users(
    skip: int = 0,
    limit: int = 100,
    role: Optional[int] = None,
    db: Session = Depends(get_db),
):
    query = db.query(User)
    if role is not None:
        query = query.filter(User.Role == role)
    return query.offset(skip).limit(limit).all()


@app.get("/users/{user_id}", response_model=schemas.User)
def read_user(user_id: int, db: Session = Depends(get_db)):
    return get_entity_or_404(db, User, user_id, "User")


@app.post("/users", response_model=schemas.User, status_code=status.HTTP_201_CREATED)
def create_user(user: schemas.UserCreate, db: Session = Depends(get_db)):
    ensure_supported_user_role(user.Role)
    ensure_foreign_keys_exist(
        db, teacher_id=user.TeacherId, class_id=user.AcademicClassId
    )

    db_user = User(**user.model_dump())
    db.add(db_user)
    commit_session(db, "Failed to create user due to a data conflict")
    db.refresh(db_user)
    return db_user


@app.patch("/users/{user_id}", response_model=schemas.User)
def update_user(user_id: int, user: schemas.UserUpdate, db: Session = Depends(get_db)):
    db_user = get_entity_or_404(db, User, user_id, "User")
    payload = user.model_dump(exclude_unset=True)

    if "Role" in payload:
        ensure_supported_user_role(payload.get("Role"))

    ensure_foreign_keys_exist(
        db, teacher_id=payload.get("TeacherId"), class_id=payload.get("AcademicClassId")
    )

    for field, value in payload.items():
        setattr(db_user, field, value)

    commit_session(db, "Failed to update user due to a data conflict")
    db.refresh(db_user)
    return db_user


@app.delete("/users/{user_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_user(user_id: int, db: Session = Depends(get_db)):
    db_user = get_entity_or_404(db, User, user_id, "User")
    db.delete(db_user)
    commit_session(db, "Failed to delete user due to a data conflict")
    return Response(status_code=status.HTTP_204_NO_CONTENT)


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8000)
