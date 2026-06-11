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
