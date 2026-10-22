/** A holiday destination card in the home page's Holidays tab. */
export interface HolidayDestination {
  holidayDestinationId: number;
  name: string;
  tagline: string | null;
  coverImageUrl: string | null;
  packageCount: number;
  startingPrice: number | null;
}

/** A custom holiday package. No online booking: customers enquire on WhatsApp. */
export interface HolidayPackage {
  holidayPackageId: number;
  title: string;
  summary: string | null;
  days: number;
  nights: number;
  /** "4 Days / 3 Nights". */
  durationLabel: string;
  /** "From ₹…" per person; null = "Price on request". */
  priceFrom: number | null;
  highlights: string[];
  coverImageUrl: string | null;
  holidayDestinationId: number | null;
  destinationName: string | null;
}

export interface AdminHolidayDestination {
  holidayDestinationId: number;
  name: string;
  tagline: string | null;
  coverImageUrl: string | null;
  displayOrder: number;
  isActive: boolean;
  packageCount: number;
}

export interface AdminHolidayPackage extends HolidayPackage {
  displayOrder: number;
  isActive: boolean;
}

export interface SaveHolidayDestinationRequest {
  name: string;
  tagline: string | null;
  displayOrder: number;
  isActive: boolean;
}

export interface SaveHolidayPackageRequest {
  title: string;
  summary: string | null;
  days: number;
  nights: number;
  priceFrom: number | null;
  highlights: string[];
  holidayDestinationId: number | null;
  displayOrder: number;
  isActive: boolean;
}
