import { Card, CardContent, Skeleton, Stack } from '@mui/material';
import type { ReactElement } from 'react';

/** Shown only on the first load; later searches keep the previous results on screen instead. */
export function ResultsSkeleton({ count }: { count: number }): ReactElement {
  return (
    <>
      {Array.from({ length: count }, (_, index) => (
        <Card component="li" key={index}>
          <CardContent>
            <Skeleton variant="text" width="70%" height={28} />
            <Skeleton variant="text" width="40%" />
            <Stack direction="row" sx={{ gap: 0.75, my: 1 }}>
              <Skeleton variant="rounded" width={96} height={24} />
              <Skeleton variant="rounded" width={72} height={24} />
              <Skeleton variant="rounded" width={84} height={24} />
            </Stack>
            <Skeleton variant="text" />
            <Skeleton variant="text" width="85%" />
          </CardContent>
        </Card>
      ))}
    </>
  );
}
