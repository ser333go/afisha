import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Запрещает значения, состоящие только из пробелов (как Must(!IsNullOrWhiteSpace) на сервере). */
export const nonBlankValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;
  if (typeof value !== 'string' || value.length === 0) {
    return null;
  }
  return value.trim().length > 0 ? null : { blank: true };
};
