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
  /** Null for Google sign-ups that haven't added a WhatsApp number yet. */
  phoneNumber: string | null;
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
  phoneNumber: string | null;
  email: string | null;
  isVerified: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  /** Email comes from the linked Google account — not editable. */
  emailVerified: boolean;
  googleLinked: boolean;
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
  instagramUrl: string | null;
  /** Office address (Company:Address), shown in the footer. */
  officeAddress: string | null;
  /** Directions link for the office. */
  officeMapUrl: string | null;
  /** Admin-uploaded office photo. */
  officePhotoUrl: string | null;
}
