from sqlalchemy import Column, Integer, String, ForeignKey, Enum, UniqueConstraint
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import relationship
import enum

Base = declarative_base()

class UserRole(enum.IntEnum):
    Admin = 0
    Teacher = 1
    Student = 2

class Subject(Base):
    __tablename__ = "Subjects"
    Id = Column(Integer, primary_key=True, index=True)
    Name = Column(String(100), nullable=False)

class Classroom(Base):
    __tablename__ = "Classrooms"
    Id = Column(Integer, primary_key=True, index=True)
    Number = Column(String(10), nullable=False)
    Capacity = Column(Integer)
    Type = Column(String(50))

class Teacher(Base):
    __tablename__ = "Teachers"
    Id = Column(Integer, primary_key=True, index=True)
    FullName = Column(String(150), nullable=False)
    SubjectId = Column(Integer, ForeignKey("Subjects.Id", ondelete="SET NULL"))
    ClassroomId = Column(Integer, ForeignKey("Classrooms.Id", ondelete="SET NULL"))
    
    Subject = relationship("Subject")
    Classroom = relationship("Classroom")

class AcademicClass(Base):
    __tablename__ = "AcademicClasses"
    Id = Column(Integer, primary_key=True, index=True)
    Name = Column(String(50), nullable=False)
    StudentCount = Column(Integer)
    Shift = Column(Integer)
    CuratorTeacherId = Column(Integer, ForeignKey("Teachers.Id", ondelete="SET NULL"), unique=True)
    
    CuratorTeacher = relationship("Teacher")

class Workload(Base):
    __tablename__ = "Workloads"
    Id = Column(Integer, primary_key=True, index=True)
    TeacherId = Column(Integer, ForeignKey("Teachers.Id"), nullable=False)
    SubjectId = Column(Integer, ForeignKey("Subjects.Id"), nullable=False)
    AcademicClassId = Column(Integer, ForeignKey("AcademicClasses.Id"), nullable=False)
    HoursPerWeek = Column(Integer, nullable=False)
    YearHours = Column(Integer, nullable=False, default=0)
    
    Teacher = relationship("Teacher")
    Subject = relationship("Subject")
    AcademicClass = relationship("AcademicClass")

class Lesson(Base):
    __tablename__ = "Lessons"
    Id = Column(Integer, primary_key=True, index=True)
    WeekStartDate = Column(String(10), nullable=False, default="")
    DayOfWeek = Column(Integer, nullable=False)
    LessonIndex = Column(Integer, nullable=False)
    TeacherId = Column(Integer, ForeignKey("Teachers.Id"), nullable=False)
    SubjectId = Column(Integer, ForeignKey("Subjects.Id"), nullable=False)
    AcademicClassId = Column(Integer, ForeignKey("AcademicClasses.Id"), nullable=False)
    ClassroomId = Column(Integer, ForeignKey("Classrooms.Id"))
    
    Teacher = relationship("Teacher")
    Subject = relationship("Subject")
    AcademicClass = relationship("AcademicClass")
    Classroom = relationship("Classroom")
    
    __table_args__ = (
        UniqueConstraint('AcademicClassId', 'WeekStartDate', 'DayOfWeek', 'LessonIndex', name='uix_class_lesson'),
        UniqueConstraint('TeacherId', 'WeekStartDate', 'DayOfWeek', 'LessonIndex', name='uix_teacher_lesson'),
        UniqueConstraint('ClassroomId', 'WeekStartDate', 'DayOfWeek', 'LessonIndex', name='uix_classroom_lesson'),
    )

class User(Base):
    __tablename__ = "Users"
    Id = Column(Integer, primary_key=True, index=True)
    Username = Column(String(50), nullable=False)
    Password = Column(String(100), nullable=False)
    FullName = Column(String(150), nullable=False)
    Role = Column(Integer, default=UserRole.Teacher)
    TeacherId = Column(Integer, ForeignKey("Teachers.Id", ondelete="SET NULL"))
    AcademicClassId = Column(Integer, ForeignKey("AcademicClasses.Id", ondelete="SET NULL"))
    
    Teacher = relationship("Teacher")
    AcademicClass = relationship("AcademicClass")
