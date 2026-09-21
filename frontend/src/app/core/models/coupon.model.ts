export interface AdminCoupon {
  couponCodeId: number;
  code: string;
  discountAmount: number;
  validFrom: string | null;
  validUntil: string | null;
  isActive: boolean;
  redemptionCount: number;
  createdAt: string;
}

export interface AdminCreateCouponRequest {
  code: string;
  discountAmount: number;
  validFrom: string | null;
  validUntil: string | null;
}

export interface AdminUpdateCouponRequest {
  discountAmount: number;
  validFrom: string | null;
  validUntil: string | null;
  isActive: boolean;
}
