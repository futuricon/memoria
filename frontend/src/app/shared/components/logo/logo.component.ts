import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-logo',
  standalone: true,
  templateUrl: './logo.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogoComponent {
  /** Outer SVG width/height in pixels. */
  readonly size = input<number>(24);
}
