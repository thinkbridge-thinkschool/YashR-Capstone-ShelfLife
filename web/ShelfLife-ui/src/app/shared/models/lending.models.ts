export interface LoanSummaryDto {
  loanId: string;
  memberId: string;
  memberName: string;
  memberEmail: string;
  bookTitleId: string;
  bookTitle: string;
  author: string;
  borrowedAt: string;
  dueDate: string;
  status: string;
  isOverdue: boolean;
}

export interface BorrowBookResponse {
  loanId: string;
  dueDate: string;
}

export interface MyLoanDto {
  loanId: string;
  bookTitleId: string;
  bookTitle: string;
  author: string;
  borrowedAt: string;
  dueDate: string;
  status: string;
  isOverdue: boolean;
}

export interface MyHoldDto {
  holdId: string;
  bookTitleId: string;
  bookTitle: string;
  author: string;
  status: string;
  placedAt: string;
  readyAt: string | null;
  expiresAt: string | null;
}

export interface HoldDto {
  holdId: string;
  memberId: string;
  memberName: string;
  bookTitleId: string;
  bookTitle: string;
  status: string;
  placedAt: string;
  readyAt: string | null;
  expiresAt: string | null;
}
