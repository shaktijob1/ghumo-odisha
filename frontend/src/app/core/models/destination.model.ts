export interface DestinationSummary {
  destinationId: number;
  name: string;
  slug: string;
  tagline: string | null;
  heroImageUrl: string | null;
  tripCount: number;
  startingPrice: number | null;
}

export interface DestinationDetail {
  destinationId: number;
  name: string;
  slug: string;
  tagline: string | null;
  region: string | null;
  heroImageUrl: string | null;
  aboutText: string | null;
  bestSeason: string | null;
  distanceFromBhubaneswar: string | null;
  idealDuration: string | null;
  knownFor: string | null;
  tripCount: number;
}

export interface AdminDestinationListItem {
  destinationId: number;
  name: string;
  slug: string;
  isActive: boolean;
  displayOrder: number;
  heroImageUrl: string | null;
  tripCount: number;
}

export interface AdminDestinationDetail {
  destinationId: number;
  name: string;
  slug: string;
  tagline: string | null;
  region: string | null;
  heroImageUrl: string | null;
  aboutText: string | null;
  bestSeason: string | null;
  distanceFromBhubaneswar: string | null;
  idealDuration: string | null;
  knownFor: string | null;
  isActive: boolean;
  displayOrder: number;
  createdAt: string;
  updatedAt: string;
  tripIds: number[];
  tripTitles: string[];
}

export interface CreateDestinationRequest {
  name: string;
  slug: string;
  tagline: string | null;
  region: string | null;
  aboutText: string | null;
  bestSeason: string | null;
  distanceFromBhubaneswar: string | null;
  idealDuration: string | null;
  knownFor: string | null;
  isActive: boolean;
  displayOrder: number;
}

export type UpdateDestinationRequest = CreateDestinationRequest;
