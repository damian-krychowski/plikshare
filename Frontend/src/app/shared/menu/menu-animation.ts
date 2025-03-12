import { trigger, transition, style, query, sequence, animate, stagger } from "@angular/animations";

export const MenuAnimation = trigger("menuAnimation", [
    transition(":enter", [
        style({ height: 0, overflow: "hidden" }),
        query(".app-menu__link", [
            style({ opacity: 0, transform: "translateY(-50px)" })
        ]),
        sequence([
            animate("150ms", style({ height: "*" })),
            query(".app-menu__link", [
                stagger(-50, [
                    animate("300ms ease", style({ opacity: 1, transform: "none" }))
                ])
            ])
        ])
    ]),

    transition(":leave", [
        style({ height: "*", overflow: "hidden" }),
        query(".app-menu__link", [style({ opacity: 1, transform: "none" })]),
        sequence([
            animate("150ms", style({ height: 0 }))
        ])
    ])
]);