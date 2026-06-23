export interface PagedList<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface PopularTitleDto {
  bookTitleId: string;
  title: string;
  author: string;
  borrowCount: number;
}

export interface OverdueLoanDto {
  loanId: string;
  memberId: string;
  memberName: string;
  bookTitle: string;
  dueDate: string;
  daysOverdue: number;
}

export interface MemberActivityDto {
  memberId: string;
  fullName: string;
  totalBorrows: number;
  activeLoans: number;
  overdueLoans: number;
}
