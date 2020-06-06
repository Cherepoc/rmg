import { Pattern, PatternMap } from '../composition/pattern';
import { Timeline, TimelineMap } from '../core/timeline';
import { flattenPattern } from './flatten-pattern';
import { mapObject } from '../core/object-operations';
import { sortTimeline } from '../core/timeline-operations';
import { combineTimelineMaps, TimelineCombineFunctionMap } from '../core/timeline-combine';

export function flattenPatternMap<T> (pattern: PatternMap<T>, duration: number, combineMap: TimelineCombineFunctionMap<T>): TimelineMap<T> {
  return mapObject(combineMap, (key, merger) => flattenPattern(pattern[key], duration, merger));
}

export function flattenPatternMapPattern<T> (pattern: Pattern<PatternMap<T>>, duration: number, combineMap: TimelineCombineFunctionMap<T>): TimelineMap<T> {
  let resultTimelineMap = emptyTimelineMap(combineMap);
  const patternDuration = Math.min(pattern.duration, duration);

  if (pattern.timeline) {
    const flattenedTimeline = flattenPatternMapTimeline<PatternMap<T>, T>(pattern.timeline, patternDuration, flattenPatternMap, combineMap);
    resultTimelineMap = combineTimelineMaps(flattenedTimeline, resultTimelineMap, 0, patternDuration, combineMap);
  }

  if (pattern.innerPatternTimeline) {
    const flattenedTimeline = flattenPatternMapTimeline<Pattern<PatternMap<T>>, T>(pattern.innerPatternTimeline, patternDuration, flattenPatternMapPattern, combineMap);
    resultTimelineMap = combineTimelineMaps(flattenedTimeline, resultTimelineMap, 0, patternDuration, combineMap);
  }

  return sortTimelineMap(resultTimelineMap);
}

export function flattenPatternMapTimeline<TSource, TTarget> (
  timeline: Timeline<TSource>,
  duration: number,
  flattener: (source: TSource, duration: number, combineMap: TimelineCombineFunctionMap<TTarget>) => TimelineMap<TTarget>,
  combineMap: TimelineCombineFunctionMap<TTarget>
): TimelineMap<TTarget> {
  let resultTimelineMap = emptyTimelineMap(combineMap);
  for (let i = 0; i < timeline.length; i++) {
    const timelineItem = timeline[i];
    if (timelineItem.position >= duration) {
      break;
    }

    const nextTimelineItem = timeline[i + 1];
    const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
    const flattenedItem = flattener(timelineItem.value, timelineItemDuration, combineMap);
    resultTimelineMap = combineTimelineMaps(flattenedItem, resultTimelineMap, timelineItem.position, timelineItemDuration, combineMap);
  }

  return sortTimelineMap(resultTimelineMap);
}

function emptyTimelineMap<T> (combineMap: TimelineCombineFunctionMap<T>): TimelineMap<T> {
  return mapObject(combineMap, () => []);
}

function sortTimelineMap<T> (timelineMap: TimelineMap<T>): TimelineMap<T> {
  return mapObject(timelineMap, (_, timeline) => sortTimeline(timeline));
}
