import { Routes } from '@angular/router';
import { SweepComponent } from './pages/sweep.component';
import { SettingsComponent } from './pages/settings.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'sweep' },
  { path: 'sweep', component: SweepComponent },
  { path: 'settings', component: SettingsComponent },
];
