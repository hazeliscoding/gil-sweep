import { Routes } from '@angular/router';
import { SweepComponent } from './pages/sweep.component';
import { CraftingComponent } from './pages/crafting.component';
import { RetainersComponent } from './pages/retainers.component';
import { TrendsComponent } from './pages/trends.component';
import { SettingsComponent } from './pages/settings.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'sweep' },
  { path: 'sweep', component: SweepComponent },
  { path: 'crafting', component: CraftingComponent },
  { path: 'retainers', component: RetainersComponent },
  { path: 'trends', component: TrendsComponent },
  { path: 'settings', component: SettingsComponent },
];
