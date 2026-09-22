import { inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 3000, panelClass: 'toast-success' });
  }

  error(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 6000, panelClass: 'toast-error' });
  }
}
