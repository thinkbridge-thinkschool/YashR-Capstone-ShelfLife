export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  memberId: string;
}

export interface RegisterRequest {
  email: string;
  fullName: string;
  password: string;
}
