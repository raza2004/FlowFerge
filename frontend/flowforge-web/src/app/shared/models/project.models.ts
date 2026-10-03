export interface ProjectDto {
  id: string;
  name: string;
  key: string;
  description: string | null;
  color: string;
  status: string;
  visibility: string;
  ownerId: string;
  ownerName: string | null;
  startDate: string | null;
  endDate: string | null;
  boardCount: number;
  taskCount: number;
  memberCount: number;
  createdAt: string;
}

export interface ProjectSummaryDto {
  id: string;
  name: string;
  key: string;
  color: string;
  status: string;
  openTasks: number;
  completedTasks: number;
}

export interface CreateProjectRequest {
  name: string;
  key: string;
  description?: string;
  color?: string;
  visibility: number;
}

export interface TaskCardDto {
  id: string;
  taskNumber: string;
  title: string;
  type: string;
  priority: string;
  assigneeId: string | null;
  assigneeName: string | null;
  dueDate: string | null;
  isOverdue: boolean;
  position: number;
  commentCount: number;
  labels?: LabelDto[] | null;
  parentTaskId?: string | null;
  subtaskCount?: number;
}

export interface LabelDto {
  id: string;
  name: string;
  color: string;
}

export interface TaskWatcherDto {
  userId: string;
  fullName: string;
}

export interface SubtaskDto {
  id: string;
  taskNumber: string;
  title: string;
  isCompleted: boolean;
}

export interface TaskCommentDto {
  id: string;
  authorId: string;
  authorName: string;
  content: string;
  isEdited: boolean;
  createdAt: string;
  mentionedUserIds: string[];
}

export interface TaskDetailDto {
  id: string;
  projectId: string;
  boardId: string;
  listId: string;
  taskNumber: string;
  title: string;
  description: string | null;
  type: string;
  priority: string;
  status: string;
  assigneeId: string | null;
  assigneeName: string | null;
  parentTaskId: string | null;
  dueDate: string | null;
  estimatedHours: number | null;
  storyPoints: number | null;
  isOverdue: boolean;
  isCompleted: boolean;
  createdAt: string;
  createdByName: string | null;
  labels: LabelDto[];
  watchers: TaskWatcherDto[];
  isWatching: boolean;
  subtasks: SubtaskDto[];
  comments: TaskCommentDto[];
  actualHours: number | null;
  timeEntries: TimeEntryDto[];
}

export interface UpdateTaskRequest {
  title: string;
  description: string | null;
  type: number;
  priority: number;
  dueDate: string | null;
  estimatedHours: number | null;
  storyPoints: number | null;
  boardId: string;
}

// Mirror the backend enums so the edit form can send numeric values.
export const TASK_TYPES: { value: number; label: string }[] = [
  { value: 0, label: 'Task' },
  { value: 1, label: 'Bug' },
  { value: 2, label: 'Feature' },
  { value: 3, label: 'Story' },
  { value: 4, label: 'Epic' },
  { value: 5, label: 'Improvement' }
];

export const TASK_PRIORITIES: { value: number; label: string }[] = [
  { value: 0, label: 'Lowest' },
  { value: 1, label: 'Low' },
  { value: 2, label: 'Medium' },
  { value: 3, label: 'High' },
  { value: 4, label: 'Highest' },
  { value: 5, label: 'Critical' }
];

export interface BoardListDto {
  id: string;
  name: string;
  color: string;
  position: number;
  wipLimit: number | null;
  tasks: TaskCardDto[];
  isDoneColumn?: boolean;
}

export interface ListSettings {
  name: string;
  color: string;
  wipLimit: number | null;
  isDoneColumn: boolean;
}

export interface TimeEntryDto {
  id: string;
  userId: string;
  userName: string;
  hours: number;
  workDate: string;
  note: string | null;
  createdAt: string;
}

export interface BoardDto {
  id: string;
  name: string;
  description: string | null;
  type: string;
  lists: BoardListDto[];
}

export interface CreateTaskRequest {
  projectId: string;
  boardId: string;
  listId: string;
  title: string;
  description?: string;
  type: number;
  priority: number;
}

export interface DashboardStatsDto {
  totalProjects: number;
  activeProjects: number;
  myOpenTasks: number;
  myOverdueTasks: number;
  tasksDueThisWeek: number;
  completedThisWeek: number;
}

export interface MyTaskDto {
  id: string;
  taskNumber: string;
  title: string;
  projectName: string;
  projectKey: string;
  projectColor: string;
  type: string;
  priority: string;
  status: string;
  dueDate: string | null;
  isOverdue: boolean;
  createdAt: string;
}
