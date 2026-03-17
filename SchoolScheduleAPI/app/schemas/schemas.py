from __future__ import annotations

from typing import Optional

from pydantic import BaseModel, ConfigDict


class ORMModel(BaseModel):
    model_config = ConfigDict(from_attributes=True)


class SubjectBase(BaseModel):
    Name: str


class SubjectCreate(SubjectBase):
    pass


class SubjectUpdate(BaseModel):
    Name: Optional[str] = None


class SubjectRead(ORMModel):
    Id: int
    Name: str


class ClassroomBase(BaseModel):
    Number: str
    Capacity: Optional[int] = None
    Type: Optional[str] = None


class ClassroomCreate(ClassroomBase):
    pass


class ClassroomUpdate(BaseModel):
    Number: Optional[str] = None
    Capacity: Optional[int] = None
    Type: Optional[str] = None


class ClassroomRead(ORMModel):
    Id: int
    Number: str
    Capacity: Optional[int] = None
    Type: Optional[str] = None


class TeacherBase(BaseModel):
    FullName: str
    SubjectId: Optional[int] = None
    ClassroomId: Optional[int] = None


class TeacherCreate(TeacherBase):
    pass


class TeacherUpdate(BaseModel):
    FullName: Optional[str] = None
    SubjectId: Optional[int] = None
    ClassroomId: Optional[int] = None


class TeacherRead(ORMModel):
    Id: int
    FullName: str
    SubjectId: Optional[int] = None
    ClassroomId: Optional[int] = None
    Subject: Optional[SubjectRead] = None
    Classroom: Optional[ClassroomRead] = None


class AcademicClassBase(BaseModel):
    Name: str
    StudentCount: Optional[int] = None
    Shift: Optional[int] = None
    CuratorTeacherId: Optional[int] = None


class AcademicClassCreate(AcademicClassBase):
    pass


class AcademicClassUpdate(BaseModel):
    Name: Optional[str] = None
    StudentCount: Optional[int] = None
    Shift: Optional[int] = None
    CuratorTeacherId: Optional[int] = None


class AcademicClassRead(ORMModel):
    Id: int
    Name: str
    StudentCount: Optional[int] = None
    Shift: Optional[int] = None
    CuratorTeacherId: Optional[int] = None
    CuratorTeacher: Optional[TeacherRead] = None


class WorkloadBase(BaseModel):
    TeacherId: int
    SubjectId: int
    AcademicClassId: int
    HoursPerWeek: int
    YearHours: Optional[int] = None


class WorkloadCreate(WorkloadBase):
    pass


class WorkloadUpdate(BaseModel):
    TeacherId: Optional[int] = None
    SubjectId: Optional[int] = None
    AcademicClassId: Optional[int] = None
    HoursPerWeek: Optional[int] = None
    YearHours: Optional[int] = None


class WorkloadRead(ORMModel):
    Id: int
    TeacherId: int
    SubjectId: int
    AcademicClassId: int
    HoursPerWeek: int
    YearHours: int
    RemainingHours: int = 0
    Teacher: TeacherRead
    Subject: SubjectRead
    AcademicClass: AcademicClassRead


class LessonBase(BaseModel):
    WeekStartDate: Optional[str] = None
    DayOfWeek: int
    LessonIndex: int
    TeacherId: int
    SubjectId: int
    AcademicClassId: int
    ClassroomId: Optional[int] = None


class LessonCreate(LessonBase):
    pass


class LessonUpdate(BaseModel):
    WeekStartDate: Optional[str] = None
    DayOfWeek: Optional[int] = None
    LessonIndex: Optional[int] = None
    TeacherId: Optional[int] = None
    SubjectId: Optional[int] = None
    AcademicClassId: Optional[int] = None
    ClassroomId: Optional[int] = None


class LessonRead(ORMModel):
    Id: int
    WeekStartDate: str
    DayOfWeek: int
    LessonIndex: int
    TeacherId: int
    SubjectId: int
    AcademicClassId: int
    ClassroomId: Optional[int] = None
    Teacher: TeacherRead
    Subject: SubjectRead
    AcademicClass: AcademicClassRead
    Classroom: Optional[ClassroomRead] = None


class UserBase(BaseModel):
    Username: str
    FullName: str
    Role: int
    TeacherId: Optional[int] = None
    AcademicClassId: Optional[int] = None


class UserCreate(UserBase):
    Password: str


class UserUpdate(BaseModel):
    Username: Optional[str] = None
    Password: Optional[str] = None
    FullName: Optional[str] = None
    Role: Optional[int] = None
    TeacherId: Optional[int] = None
    AcademicClassId: Optional[int] = None


class UserRead(ORMModel):
    Id: int
    Username: str
    FullName: str
    Role: int
    TeacherId: Optional[int] = None
    AcademicClassId: Optional[int] = None
    Teacher: Optional[TeacherRead] = None
    AcademicClass: Optional[AcademicClassRead] = None


# Backward-compatible aliases used by existing routes.
Subject = SubjectRead
Classroom = ClassroomRead
Teacher = TeacherRead
AcademicClass = AcademicClassRead
Workload = WorkloadRead
Lesson = LessonRead
User = UserRead
