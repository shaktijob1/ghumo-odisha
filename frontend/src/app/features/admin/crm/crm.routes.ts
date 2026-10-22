import { Routes } from '@angular/router';

/** Admin → CRM (lazy-loaded; the admin guard on the parent route already applies). */
export const CRM_ROUTES: Routes = [
  {
    path: '',
    title: 'CRM · Ghumo Odisha',
    loadComponent: () => import('./dashboard/crm-dashboard.component').then((m) => m.CrmDashboardComponent),
  },
  {
    path: 'customers',
    title: 'CRM customers · Ghumo Odisha',
    loadComponent: () => import('./customers/crm-customer-list.component').then((m) => m.CrmCustomerListComponent),
  },
  {
    path: 'customers/new',
    title: 'Add customer · Ghumo Odisha',
    loadComponent: () => import('./customers/crm-customer-form.component').then((m) => m.CrmCustomerFormComponent),
  },
  {
    path: 'customers/import',
    title: 'Import leads · Ghumo Odisha',
    loadComponent: () => import('./import/crm-import.component').then((m) => m.CrmImportComponent),
  },
  {
    path: 'customers/:id',
    title: 'Customer · Ghumo Odisha',
    loadComponent: () => import('./customers/crm-customer-detail.component').then((m) => m.CrmCustomerDetailComponent),
  },
  {
    path: 'customers/:id/edit',
    title: 'Edit customer · Ghumo Odisha',
    loadComponent: () => import('./customers/crm-customer-form.component').then((m) => m.CrmCustomerFormComponent),
  },
  {
    path: 'departures',
    title: 'Trips / Campaigns · Ghumo Odisha',
    loadComponent: () => import('./departures/crm-departure-list.component').then((m) => m.CrmDepartureListComponent),
  },
  {
    path: 'departures/:id',
    title: 'Departure · Ghumo Odisha',
    loadComponent: () => import('./departures/crm-departure-detail.component').then((m) => m.CrmDepartureDetailComponent),
  },
  {
    path: 'pipeline',
    title: 'Sales pipeline · Ghumo Odisha',
    loadComponent: () => import('./pipeline/crm-pipeline.component').then((m) => m.CrmPipelineComponent),
  },
  {
    path: 'follow-ups',
    title: 'Follow-ups · Ghumo Odisha',
    loadComponent: () => import('./followups/crm-followups.component').then((m) => m.CrmFollowUpsComponent),
  },
  {
    path: 'whatsapp-templates',
    title: 'WhatsApp templates · Ghumo Odisha',
    loadComponent: () => import('./templates/crm-templates.component').then((m) => m.CrmTemplatesComponent),
  },
  {
    path: 'communications',
    title: 'Communication history · Ghumo Odisha',
    loadComponent: () => import('./communications/crm-communications.component').then((m) => m.CrmCommunicationsComponent),
  },
  {
    path: 'reports',
    title: 'CRM reports · Ghumo Odisha',
    loadComponent: () => import('./reports/crm-reports.component').then((m) => m.CrmReportsComponent),
  },
];
