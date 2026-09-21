export interface AdminAuthResponse {
  token: string;
  adminUserId: number;
  username: string;
  role: string;
}

export interface CustomerAuthResponse {
  token: string;
  refreshToken: string;
  customerId: number;
  name: string;
  phoneNumber: string;
  email: string | null;
}

export interface RequestOtpRequest {
  name?: string | null;
  whatsAppNumber: string;
}

export interface RequestOtpResponse {
  otpLength: number;
}

export interface VerifyOtpRequest {
  whatsAppNumber: string;
  otp: string;
}

export interface CustomerProfile {
  customerId: number;
  name: string;
  phoneNumber: string;
  email: string | null;
  isVerified: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface UpdateProfileRequest {
  name: string;
  email?: string | null;
}

export interface ContactInfo {
  name: string;
  role: string;
  phone: string;
  whatsAppNumber: string;
  email: string;
  photoUrl: string | null;
}
