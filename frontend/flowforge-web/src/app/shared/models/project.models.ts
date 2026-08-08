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
}

export interface BoardListDto {
  id: string;
  name: string;
  color: string;
  position: number;
  wipLimit: number | null;
  tasks: TaskCardDto[];
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
