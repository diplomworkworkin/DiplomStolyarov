from __future__ import annotations

from dataclasses import dataclass
from datetime import date, timedelta
import os

from sqlalchemy.orm import Session

from app.core.security import hash_password
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


SUBJECT_NAMES = [
    "Математика",
    "Русский язык",
    "Литература",
    "История",
    "Физика",
    "Химия",
    "Биология",
    "География",
    "Информатика",
    "Английский язык",
    "Физическая культура",
]

CLASSROOM_SPECS = [
    ("7A-RM", 32, "Обычный"),
    ("8B-RM", 32, "Обычный"),
    ("9A-RM", 30, "Обычный"),
    ("10A-RM", 30, "Обычный"),
    ("301", 28, "Обычный"),
    ("302", 28, "Обычный"),
    ("401", 25, "Лаборатория"),
    ("402", 25, "Лаборатория"),
]

CLASS_SPECS = [
    ("7А", 27, 1),
    ("8Б", 25, 1),
    ("9А", 24, 1),
    ("10А", 22, 1),
]

# 5 дней x 6 уроков = 30 уроков на класс.
WEEK_TEMPLATE = {
    1: ["Математика", "Русский язык", "Литература", "История", "Английский язык", "Физическая культура"],
    2: ["Математика", "Русский язык", "Физика", "География", "Информатика", "Литература"],
    3: ["Математика", "Русский язык", "Химия", "Биология", "Английский язык", "Литература"],
    4: ["Математика", "Физика", "Химия", "История", "География", "Физическая культура"],
    5: ["Математика", "Русский язык", "Физика", "Биология", "Английский язык", "Информатика"],
}


@dataclass(frozen=True)
class ClassContext:
    academic_class: AcademicClass
    classroom: Classroom


def seed_database(db: Session) -> None:
    force_reset = is_truthy(os.getenv("SEED_RESET_ON_START"))
    has_existing_data = database_has_any_data(db)

    # Keep user changes between API restarts.
    # Reseed only when DB is empty, or when forced by env var.
    if has_existing_data and not force_reset:
        ensure_supported_roles_only(db)
        db.commit()
        return

    if force_reset:
        cleanup_legacy_test_data(db)

    subjects = ensure_subjects(db)
    classrooms = ensure_classrooms(db)
    class_contexts = ensure_classes(db, classrooms)
    teachers_by_key = ensure_teachers(db, subjects, class_contexts)
    ensure_workloads(db, subjects, class_contexts, teachers_by_key)
    ensure_lessons(db, subjects, class_contexts, teachers_by_key)
    ensure_users(db)
    ensure_supported_roles_only(db)
    db.commit()


def database_has_any_data(db: Session) -> bool:
    return any(
        (
            db.query(Subject.Id).first() is not None,
            db.query(Classroom.Id).first() is not None,
            db.query(AcademicClass.Id).first() is not None,
            db.query(Teacher.Id).first() is not None,
            db.query(User.Id).first() is not None,
            db.query(Workload.Id).first() is not None,
            db.query(Lesson.Id).first() is not None,
        )
    )


def is_truthy(value: str | None) -> bool:
    if value is None:
        return False
    return value.strip().lower() in {"1", "true", "yes", "on"}


def ensure_supported_roles_only(db: Session) -> None:
    allowed_roles = {int(UserRole.Admin), int(UserRole.Teacher)}
    unsupported_users = db.query(User).filter(~User.Role.in_(allowed_roles)).all()
    for user in unsupported_users:
        db.delete(user)


def cleanup_legacy_test_data(db: Session) -> None:
    seed_subject_names = set(SUBJECT_NAMES)
    seed_classroom_numbers = {x[0] for x in CLASSROOM_SPECS}
    seed_class_names = {x[0] for x in CLASS_SPECS}
    seed_teacher_names = build_expected_teacher_names()

    # Всегда перестраиваем нагрузку и уроки с нуля, чтобы не тянуть старые конфликты.
    db.query(Lesson).delete(synchronize_session=False)
    db.query(Workload).delete(synchronize_session=False)

    users = db.query(User).order_by(User.Id).all()
    admin_to_keep = next((x for x in users if x.Role == int(UserRole.Admin)), None)
    if admin_to_keep is None:
        admin_to_keep = next((x for x in users if x.Username.lower() == "admin"), None)

    for user in users:
        if admin_to_keep is not None and user.Id == admin_to_keep.Id:
            user.TeacherId = None
            user.AcademicClassId = None
            continue
        db.delete(user)

    teachers_to_delete = (
        db.query(Teacher)
        .filter(~Teacher.FullName.in_(seed_teacher_names))
        .all()
    )
    teacher_ids_to_delete = {x.Id for x in teachers_to_delete}
    if teacher_ids_to_delete:
        db.query(AcademicClass).filter(
            AcademicClass.CuratorTeacherId.in_(teacher_ids_to_delete)
        ).update({AcademicClass.CuratorTeacherId: None}, synchronize_session=False)
        for teacher in teachers_to_delete:
            db.delete(teacher)

    classes_to_delete = (
        db.query(AcademicClass)
        .filter(~AcademicClass.Name.in_(seed_class_names))
        .all()
    )
    for academic_class in classes_to_delete:
        db.delete(academic_class)

    subjects_to_delete = (
        db.query(Subject)
        .filter(~Subject.Name.in_(seed_subject_names))
        .all()
    )
    subject_ids_to_delete = {x.Id for x in subjects_to_delete}
    if subject_ids_to_delete:
        db.query(Teacher).filter(
            Teacher.SubjectId.in_(subject_ids_to_delete)
        ).update({Teacher.SubjectId: None}, synchronize_session=False)
        for subject in subjects_to_delete:
            db.delete(subject)

    classrooms_to_delete = (
        db.query(Classroom)
        .filter(~Classroom.Number.in_(seed_classroom_numbers))
        .all()
    )
    classroom_ids_to_delete = {x.Id for x in classrooms_to_delete}
    if classroom_ids_to_delete:
        db.query(Teacher).filter(
            Teacher.ClassroomId.in_(classroom_ids_to_delete)
        ).update({Teacher.ClassroomId: None}, synchronize_session=False)
        for classroom in classrooms_to_delete:
            db.delete(classroom)

    db.flush()


def ensure_subjects(db: Session) -> dict[str, Subject]:
    by_name = {x.Name: x for x in db.query(Subject).all()}

    for name in SUBJECT_NAMES:
        if name not in by_name:
            entity = Subject(Name=name)
            db.add(entity)
            db.flush()
            by_name[name] = entity

    return by_name


def ensure_classrooms(db: Session) -> dict[str, Classroom]:
    by_number = {x.Number: x for x in db.query(Classroom).all()}

    for number, capacity, room_type in CLASSROOM_SPECS:
        classroom = by_number.get(number)
        if classroom is None:
            classroom = Classroom(Number=number, Capacity=capacity, Type=room_type)
            db.add(classroom)
            db.flush()
            by_number[number] = classroom
        else:
            classroom.Capacity = capacity
            classroom.Type = room_type

    return by_number


def ensure_classes(db: Session, classrooms: dict[str, Classroom]) -> dict[str, ClassContext]:
    by_name = {x.Name: x for x in db.query(AcademicClass).all()}
    contexts: dict[str, ClassContext] = {}

    # Для seed у каждого класса свой кабинет, чтобы не ловить пересечения по аудиториям.
    room_order = ["7A-RM", "8B-RM", "9A-RM", "10A-RM"]

    for index, (name, student_count, shift) in enumerate(CLASS_SPECS):
        academic_class = by_name.get(name)
        if academic_class is None:
            academic_class = AcademicClass(Name=name, StudentCount=student_count, Shift=shift)
            db.add(academic_class)
            db.flush()
            by_name[name] = academic_class
        else:
            academic_class.StudentCount = student_count
            academic_class.Shift = shift

        class_room = classrooms[room_order[index]]
        contexts[name] = ClassContext(academic_class=academic_class, classroom=class_room)

    return contexts


def ensure_teachers(
    db: Session,
    subjects: dict[str, Subject],
    class_contexts: dict[str, ClassContext],
) -> dict[tuple[str, str], Teacher]:
    by_full_name = {x.FullName: x for x in db.query(Teacher).all()}
    by_key: dict[tuple[str, str], Teacher] = {}

    for class_name, context in class_contexts.items():
        for subject_name in SUBJECT_NAMES:
            full_name = f"Учитель {subject_name} {class_name}"
            teacher = by_full_name.get(full_name)
            if teacher is None:
                teacher = Teacher(
                    FullName=full_name,
                    SubjectId=subjects[subject_name].Id,
                    ClassroomId=context.classroom.Id,
                )
                db.add(teacher)
                db.flush()
                by_full_name[full_name] = teacher
            else:
                teacher.SubjectId = subjects[subject_name].Id
                teacher.ClassroomId = context.classroom.Id

            by_key[(class_name, subject_name)] = teacher

        # Классный руководитель: учитель математики этого класса.
        context.academic_class.CuratorTeacherId = by_key[(class_name, "Математика")].Id

    return by_key


def ensure_workloads(
    db: Session,
    subjects: dict[str, Subject],
    class_contexts: dict[str, ClassContext],
    teachers_by_key: dict[tuple[str, str], Teacher],
) -> None:
    existing = {
        (x.TeacherId, x.SubjectId, x.AcademicClassId): x
        for x in db.query(Workload).all()
    }

    hours_by_subject = calculate_weekly_hours_from_template()

    for class_name, context in class_contexts.items():
        for subject_name, hours in hours_by_subject.items():
            teacher = teachers_by_key[(class_name, subject_name)]
            subject = subjects[subject_name]
            key = (teacher.Id, subject.Id, context.academic_class.Id)
            workload = existing.get(key)

            if workload is None:
                workload = Workload(
                    TeacherId=teacher.Id,
                    SubjectId=subject.Id,
                    AcademicClassId=context.academic_class.Id,
                    HoursPerWeek=hours,
                    YearHours=hours * 34,
                )
                db.add(workload)
                existing[key] = workload
            else:
                workload.HoursPerWeek = hours
                if getattr(workload, "YearHours", 0) <= 0:
                    workload.YearHours = hours * 34


def ensure_lessons(
    db: Session,
    subjects: dict[str, Subject],
    class_contexts: dict[str, ClassContext],
    teachers_by_key: dict[tuple[str, str], Teacher],
) -> None:
    week_start = current_week_start_iso()
    existing = {
        (x.WeekStartDate, x.AcademicClassId, x.DayOfWeek, x.LessonIndex): x
        for x in db.query(Lesson).all()
    }

    for class_name, context in class_contexts.items():
        for day_of_week, day_subjects in WEEK_TEMPLATE.items():
            for lesson_index, subject_name in enumerate(day_subjects, start=1):
                teacher = teachers_by_key[(class_name, subject_name)]
                subject = subjects[subject_name]
                key = (week_start, context.academic_class.Id, day_of_week, lesson_index)
                lesson = existing.get(key)

                if lesson is None:
                    lesson = Lesson(
                        WeekStartDate=week_start,
                        DayOfWeek=day_of_week,
                        LessonIndex=lesson_index,
                        TeacherId=teacher.Id,
                        SubjectId=subject.Id,
                        AcademicClassId=context.academic_class.Id,
                        ClassroomId=context.classroom.Id,
                    )
                    db.add(lesson)
                    existing[key] = lesson
                else:
                    lesson.WeekStartDate = week_start
                    lesson.TeacherId = teacher.Id
                    lesson.SubjectId = subject.Id
                    lesson.ClassroomId = context.classroom.Id


def ensure_users(db: Session) -> None:
    users = db.query(User).all()

    admin = next((x for x in users if x.Role == int(UserRole.Admin)), None)
    if admin is None:
        admin = next((x for x in users if x.Username == "admin"), None)

    if admin is None:
        admin = User(
            Username="admin",
            Password=hash_password("admin"),
            FullName="Администратор",
            Role=int(UserRole.Admin),
        )
        db.add(admin)
    else:
        admin.Username = "admin"
        admin.Password = hash_password("admin")
        admin.FullName = "Администратор"
        admin.Role = int(UserRole.Admin)
        admin.TeacherId = None
        admin.AcademicClassId = None

    # teacher{id} / teacher{id}
    teacher_users_by_id = {
        x.TeacherId: x
        for x in users
        if x.TeacherId is not None and x.Role == int(UserRole.Teacher)
    }

    all_teachers = db.query(Teacher).order_by(Teacher.Id).all()
    for teacher in all_teachers:
        user = teacher_users_by_id.get(teacher.Id)
        if user is None:
            user = next((x for x in users if x.Username == f"teacher{teacher.Id}"), None)

        if user is None:
            user = User(
                Username=f"teacher{teacher.Id}",
                Password=hash_password(f"teacher{teacher.Id}"),
                FullName=teacher.FullName,
                Role=int(UserRole.Teacher),
                TeacherId=teacher.Id,
            )
            db.add(user)
            users.append(user)
        else:
            user.Username = f"teacher{teacher.Id}"
            user.Password = hash_password(f"teacher{teacher.Id}")
            user.FullName = teacher.FullName
            user.Role = int(UserRole.Teacher)
            user.TeacherId = teacher.Id
            user.AcademicClassId = None

    # Если ID не с 1, оставляем предсказуемый fallback-аккаунт teacher1/teacher1.
    if all_teachers and not any(x.Username == "teacher1" for x in users):
        fallback_teacher = all_teachers[0]
        fallback_user = User(
            Username="teacher1",
            Password=hash_password("teacher1"),
            FullName=fallback_teacher.FullName,
            Role=int(UserRole.Teacher),
            TeacherId=fallback_teacher.Id,
        )
        db.add(fallback_user)
        users.append(fallback_user)

    ensure_supported_roles_only(db)

def build_expected_teacher_names() -> set[str]:
    names: set[str] = set()
    for class_name, _, _ in CLASS_SPECS:
        for subject_name in SUBJECT_NAMES:
            names.add(f"Учитель {subject_name} {class_name}")
    return names


def calculate_weekly_hours_from_template() -> dict[str, int]:
    hours: dict[str, int] = {name: 0 for name in SUBJECT_NAMES}

    for day_subjects in WEEK_TEMPLATE.values():
        for subject_name in day_subjects:
            hours[subject_name] = hours.get(subject_name, 0) + 1

    return hours


def current_week_start_iso() -> str:
    today = date.today()
    monday = today - timedelta(days=today.weekday())
    return monday.isoformat()
