import { Routes } from '@angular/router';
import { SweepComponent } from './pages/sweep.component';
import { RetainersComponent } from './pages/retainers.component';
import { SettingsComponent } from './pages/settings.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'sweep' },
  { path: 'sweep', component: SweepComponent },
  { path: 'retainers', component: RetainersComponent },
  { path: 'settings', component: SettingsComponent },
];
