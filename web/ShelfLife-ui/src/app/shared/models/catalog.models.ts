export interface AddBookResponse {
  id: string;
}

export interface AddCopyResponse {
  id: string;
}

export interface BookSummaryDto {
  bookTitleId: string;
  title: string;
  author: string;
  isbn: string;
  status: string;
  availableCopies: number;
  totalCopies: number;
}
